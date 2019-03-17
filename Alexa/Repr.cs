using System;
using System.Collections.Generic;
using System.Text;
using Alexa.NET.Response;

namespace KLabSkill.Alexa
{
    public class Repr : Reprompt
    {
        public Repr(string text) : base(text)
        {

        }

        public Repr(IOutputSpeech speech)
        {
            OutputSpeech = speech;
        }
    }
}
