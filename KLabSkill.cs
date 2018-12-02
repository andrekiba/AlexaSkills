using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alexa.NET;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;
using AlexaSkills.Extensions;
using HtmlAgilityPack;
using Meetup.NetStandard;
using Meetup.NetStandard.Request.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AlexaSkills
{
    public static class KLabSkill
    {
        [FunctionName("KLabSkill")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]HttpRequest req, ILogger log)
        {
            var json = await req.ReadAsStringAsync();
            var skillRequest = JsonConvert.DeserializeObject<SkillRequest>(json);

            // Verifies that the request is indeed coming from Alexa.
            var isValid = await skillRequest.ValidateRequest(req, log);
            if (!isValid)
            {
                return new BadRequestResult();
            }

            var request = skillRequest.Request;
            SkillResponse response = null;

            try
            {
                switch (request)
                {
                    case LaunchRequest launchRequest:
                        log.LogInformation("Session started");
                        response = ResponseBuilder.Tell("Ciao! Benvenuto in KLab Community. Cosa desideri sapere?");
                        response.Response.ShouldEndSession = false;
                        break;
                    case IntentRequest intentRequest:
                        // Checks whether to handle system messages defined by Amazon.
                        var systemIntentResponse = HandleSystemIntentRequest(intentRequest);
                        if (systemIntentResponse.IsHandled)
                        {
                            response = systemIntentResponse.Response;
                        }
                        else
                        {
                            // Processes request according to intentRequest.Intent.Name...
                            response = await GetResponse(intentRequest, log);
                        }

                        break;
                    case SessionEndedRequest sessionEndedRequest:
                        log.LogInformation("Session ended");
                        response = ResponseBuilder.Empty();
                        response.Response.ShouldEndSession = true;
                        break;
                }
            }
            catch
            {
                response = ResponseBuilder.Tell("Mi dispiace, c'è stato un errore inatteso. Per favore, riprova più tardi.");
            }

            return new OkObjectResult(response);
        }

        private static (bool IsHandled, SkillResponse Response) HandleSystemIntentRequest(IntentRequest request)
        {
            SkillResponse response = null;
            switch (request.Intent.Name)
            {
                case "AMAZON.CancelIntent":
                    response = ResponseBuilder.Tell("Canceling...");
                    break;
                case "AMAZON.HelpIntent":
                    response = ResponseBuilder.Tell("Help...");
                    response.Response.ShouldEndSession = false;
                    break;
                case "AMAZON.StopIntent":
                    response = ResponseBuilder.Tell("Stopping...");
                    break;
                default:
                    break;
            }

            return (response != null, response);
        }

        private static async Task<SkillResponse> GetResponse(IntentRequest request, ILogger log)
        {
            const string apiToken = "342d206c6c7e106f267831734b387745";
            var meetup = MeetupClient.WithApiToken(apiToken);

            try
            {
                var eventRequest = new GetEventsRequest("KLab-Community")
                {
                    Status = EventStatus.Upcoming,
                    PageSize = 1
                };

                var eventResponse = await meetup.Events.For(eventRequest);

                if (!eventResponse.Data.Any())
                {
                    return ResponseBuilder.Tell("Non ci sono eventi in programmazione per ora. Riprova fra qualche giorno.");
                }

                SkillResponse response = null;
                var eventData = eventResponse.Data.First();
                switch (request.Intent.Name)
                {
                    case "NextEventIntent":
                        var reprompt = new Reprompt
                        {
                            OutputSpeech = new PlainTextOutputSpeech
                            {
                                Text = "Desideri sapere altro?"
                            }
                        };
                        response = ResponseBuilder.Ask($"Il prossimo KLab sarà il {eventData.LocalDate} alle ore {eventData.LocalTime} presso {eventData.Venue.Name}", reprompt);
                        break;
                    case "NextEventDetailsIntent":
                        response = ResponseBuilder.Tell($"Ecco i dettagli dell'evento.\n\r{StripEventDescription(eventData.Description)}");
                        break;
                    default:
                        break;
                }

                return response;

            }
            catch (Exception e)
            {
                var error = $"{e.Message}\n\r{e.StackTrace}";
                log.LogError(error);
                return ResponseBuilder.Tell("Purtroppo non riesco a recuperare informazioni inerenti al prossimo KLab. Riprova più tardi.");
            }
        }

        private static string StripEventDescription(string eventDesc)
        {
            var document = new  HtmlDocument();
            document.LoadHtml(eventDesc);
            var ps = document.DocumentNode.SelectNodes("//p");
            var goodPs = ps.Skip(1)
                .Take(ps.Count - 4)
                .Select(p => HtmlEntity.DeEntitize(p.InnerText))
                .Select(p => p.Replace("~", "tenuto da"))
                .Select(p => Regex.Replace(p, "(\\d+).(\\d+)", "$1:$2"));

            var stripped = string.Join("\n\r", goodPs);

            return stripped;
        }
    }
}
