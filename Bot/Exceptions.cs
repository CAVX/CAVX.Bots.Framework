using System;
using System.Collections.Generic;
using System.Text;

namespace Bot
{
    public class UserNotFoundException : Exception
    {
        public UserNotFoundException() : base("I can't find that user!") { }
    }
    public class CommandInvalidException : Exception
    {
        public CommandInvalidException() : base() { }
    }
    public class CommandParameterValidationException : Exception
    {
        public CommandParameterValidationException(string message) : base(message) { }
    }

}
