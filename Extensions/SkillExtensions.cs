using System;
using System.Linq;
using System.Threading.Tasks;
using Alexa.NET.Request;
using Alexa.NET.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace KLabSkill.Extensions
{
    internal static class RequestValidationExtensions
    {
        const int AllowedTimestampToleranceInSeconds = 150;

        public static async Task<bool> ValidateRequest(this SkillRequest skillRequest, HttpRequest request, ILogger log)
        {
            // Verifies that the request is indeed coming from Alexa.

            request.Headers.TryGetValue("SignatureCertChainUrl", out var signatureChainUrl);
            if (string.IsNullOrWhiteSpace(signatureChainUrl))
            {
                log.LogError("Validation failed. Empty SignatureCertChainUrl header");
                return false;
            }

            Uri certUrl;
            try
            {
                certUrl = new Uri(signatureChainUrl);
            }
            catch
            {
                log.LogError($"Validation failed. SignatureChainUrl not valid: {signatureChainUrl}");
                return false;
            }

            request.Headers.TryGetValue("Signature", out var signature);
            if (string.IsNullOrWhiteSpace(signature))
            {
                log.LogError("Validation failed - Empty Signature header");
                return false;
            }

            request.Body.Position = 0;
            var body = await request.ReadAsStringAsync();
            request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(body))
            {
                log.LogError("Validation failed - the JSON is empty");
                return false;
            }

            var isTimestampValid = RequestTimestampWithinTolerance(skillRequest.Request.Timestamp);
            var isValid = await RequestVerification.Verify(signature, certUrl, body);

            if (isValid && isTimestampValid)
                return true;

            log.LogError("Validation failed - RequestVerification failed");
            return false;

        }

        static bool RequestTimestampWithinTolerance(DateTime timestamp)
            => Math.Abs(DateTimeOffset.Now.Subtract(timestamp).TotalSeconds) <= AllowedTimestampToleranceInSeconds;
    }

    public static class StringExtensions
    {
        public static bool IsNullOrWhiteSpace(this string s) => string.IsNullOrWhiteSpace(s);

        public static SsmlOutputSpeech ToSsmlSpeech(this string text) => new SsmlOutputSpeech { Ssml = $"<speak>{text}</speak>" };

        public static SsmlOutputSpeech P(this string text) => new SsmlOutputSpeech { Ssml = $"<p>{text}</p>" };
    }

    internal static class SlotExtensions
    {
        public static string Dump(this Slot slot)
        {
            if (slot.Value.IsNullOrWhiteSpace() || slot.Resolution is null || !slot.Resolution.Authorities.Any())
                return $"Slot {slot.Name} error";
            var dump = string.Join(Environment.NewLine, slot.Name, slot.ConfirmationStatus, slot.Value,
                slot.Resolution.Authorities.Select(a => a.Dump()));

            return dump;
        }

        public static string Dump(this ResolutionAuthority ra)
        {
            var dump = string.Join(Environment.NewLine, ra.Name, ra.Status.Code, ra.Values.Select(v => v.Dump()));
            return dump;
        }

        public static string Dump(this ResolutionValueContainer rvc)
        {
            var dump = $"{rvc.Value.Id} - {rvc.Value.Name}";

            return dump;
        }
    }
}
