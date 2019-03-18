using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alexa.NET;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;
using HtmlAgilityPack;
using KLabSkill.Alexa;
using KLabSkill.Configuration;
using KLabSkill.Extensions;
using Meetup.NetStandard;
using Meetup.NetStandard.Request.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace KLabSkill
{
    public class KLabSkill
    {
        #region Fields

        const string BreakStrong = "<break strength=\"strong\"/>";
        const string BreakMedium = "<break strength=\"medium\"/>";
        const string Break = "<break/>";
        readonly MeetupClient meetup;

        #endregion 

        public KLabSkill()
        {           
            meetup = MeetupClient.WithApiToken(AppSettings.MeetupApiToken);
        }

        [FunctionName("KLabSkill")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]HttpRequest req, ILogger log)
        {
            var json = await req.ReadAsStringAsync();
            var skillRequest = JsonConvert.DeserializeObject<SkillRequest>(json);

            // Verifies that the request is indeed coming from Alexa.
            var isValid = await skillRequest.ValidateRequest(req, log);
            if (!isValid)
                return new BadRequestResult();

            var session = skillRequest.Session;
            var request = skillRequest.Request;
            SkillResponse response = null;

            try
            {
                switch (request)
                {
                    case LaunchRequest launchRequest:
                        response = HandleLaunchRequest(launchRequest, session);
                        break;
                    case IntentRequest intentRequest when intentRequest.Intent.Name == Intents.HelpIntent:
                        response = HandleHelpIntent(intentRequest, session);
                        break;
                    case IntentRequest intentRequest when intentRequest.Intent.Name == Intents.StopIntent ||
                                                          intentRequest.Intent.Name == Intents.CancelIntent:
                        response = HandleCancelStopIntent(intentRequest, session);
                        break;
                    case IntentRequest intentRequest when CanHandleNextInventIntent(intentRequest, session):
                        response = await HandleNextInventIntent(intentRequest, session);
                        break;
                    case IntentRequest intentRequest: //Unhandled
                        response = HandleUnhandled(intentRequest);
                        break;
                    case SessionEndedRequest sessionEndedRequest:
                        response = HandleSessionEndedRequest(sessionEndedRequest);
                        break;
                }
            }
            catch
            {
                response = ResponseBuilder.Tell("Purtroppo non riesco a recuperare informazioni inerenti al prossimo KLab. Riprova più tardi.");
            }

            return new OkObjectResult(response);
        }


        #region Methods

        #region Launch - End

        static SkillResponse HandleLaunchRequest(LaunchRequest request, Session session)
        {
            var reprompt = new Repr($"Ad esempio puoi dirmi, {BreakStrong} Quando sarà il prossimo evento? Oppure, {BreakStrong} Quali sono i dettagli del prossimo evento?".ToSsmlSpeech());
            var response = ResponseBuilder.Ask("Ciao! Benvenuto in KLab Community. Cosa desideri sapere?".ToSsmlSpeech(), reprompt);
            response.Response.ShouldEndSession = false;
            return response;
        }
        static SkillResponse HandleSessionEndedRequest(SessionEndedRequest request)
        {
            var response = ResponseBuilder.Empty();
            response.Response.ShouldEndSession = true;
            return response;
        }

        #endregion

        #region Help - Cancel - Stop

        static SkillResponse HandleHelpIntent(IntentRequest request, Session session)
        {
            var response = ResponseBuilder.Tell($"Ad esempio prova a dirmi {BreakStrong} Quando sarà il prossimo evento?".ToSsmlSpeech());
            response.Response.ShouldEndSession = false;
            return response;
        }

        static SkillResponse HandleCancelStopIntent(IntentRequest request, Session session)
        {
            var response = ResponseBuilder.Tell("OK, ci vediamo al prossimo KLab!");
            response.Response.ShouldEndSession = true;
            return response;
        }

        #endregion

        #region Events

        static bool CanHandleNextInventIntent(IntentRequest request, Session session)
        {
            return request.Intent.Name == Intents.NextEventIntent || request.Intent.Name == Intents.NextEventDetailsIntent;
        }

        async Task<SkillResponse> HandleNextInventIntent(IntentRequest request, Session session)
        {
            SkillResponse response;

            var eventRequest = new GetEventsRequest("KLab-Community")
            {
                Status = EventStatus.Upcoming,
                PageSize = 1
            };
            var eventResponse = await meetup.Events.For(eventRequest);

            if (!eventResponse.Data.Any())
                response = ResponseBuilder.Tell("Non ci sono eventi in programmazione per ora. Riprova fra qualche giorno.");            
            else
            {
                var eventData = eventResponse.Data.First();

                if (request.Intent.Name == Intents.NextEventIntent)
                {
                    
                    var repr = new Repr("Desideri sapere altro?");
                    response = ResponseBuilder.Ask(($"Il prossimo KLab sarà il {eventData.LocalDate}, " +
                                                   $"{BreakMedium} alle ore {eventData.LocalTime} presso {eventData.Venue.Name}").ToSsmlSpeech(), repr);
                }
                else
                {
                    response = ResponseBuilder.Tell($"Ecco i dettagli dell'evento.\n\r{StripEventDescription(eventData.Description)}");
                }
            }

            return response;
        }

        static string StripEventDescription(string eventDesc)
        {
            var document = new HtmlDocument();
            document.LoadHtml(eventDesc);
            var ps = document.DocumentNode.SelectNodes("//p");
            var goodPs = ps.Take(ps.Count - 2)
                .Select(p => HtmlEntity.DeEntitize(p.InnerHtml))
                .Select(p => p.Replace("<br>", "\n\r"))
                .Select(p => p.Replace(" ~ ", ", tenuto da"))
                //.Select(p => Regex.Replace(p, @"\(\d{4}\)", "<say-as interpret-as=\"time\">$1</say-as>"))                        
                .Select(p => Regex.Replace(p, "(\\d{2}).(\\d{2})", "$1:$2"));

            var stripped = string.Join("\n\r", goodPs);

            return stripped;
        }

        #endregion

        #region Unhandled 

        static SkillResponse HandleUnhandled(IntentRequest request)
        {
            var reprompt = new Repr($"Se hai bisogno di aiuto prova a dire {BreakMedium} Alexa, aiuto.");
            var response = ResponseBuilder.Ask("Non ho capito cosa mi hai chiesto? Per favore, dimmelo di nuovo.".ToSsmlSpeech(), reprompt);
            return response;
        }

        #endregion

        #endregion
    }
}
