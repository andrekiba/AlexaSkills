using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Alexa.NET.Response;
using Alexa.NET.Request.Type;
using Alexa.NET;
using Alexa.NET.Request;
using AlexaSkills.Extensions;

namespace AlexaSkills
{
    public static class SvegliaPigroneSkill
    {
        [FunctionName("SvegliaPigroneSkill")]
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
                if (request is LaunchRequest launchRequest)
                {
                    log.LogInformation("Session started");
                    response = ResponseBuilder.Tell("Your welcome message.");
                    response.Response.ShouldEndSession = false;
                }
                else if (request is IntentRequest intentRequest)
                {
                    // Checks whether to handle system messages defined by Amazon.
                    var systemIntentResponse = HandleSystemIntentRequest(intentRequest);
                    if (systemIntentResponse.IsHandled)
                    {
                        response = systemIntentResponse.Response;
                    }
                    else
                    {
                        // Processes request according to intentRequest.Intent.Name...
                        response = ResponseBuilder.Tell("Sveglia pigrone!");
                    }
                }
                else if (request is SessionEndedRequest sessionEndedRequest)
                {
                    log.LogInformation("Session ended");
                    response = ResponseBuilder.Empty();
                    response.Response.ShouldEndSession = true;
                }
            }
            catch
            {
                response = ResponseBuilder.Tell("I'm sorry, there was an unexpected error. Please, try again later.");
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
            }

            return (response != null, response);
        }
    }
}
