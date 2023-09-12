using System;

namespace CAVX.Bots.Framework
{
    public class UserNotFoundException : Exception
    {
        public UserNotFoundException() : base("I can't find that user!")
        {
        }

        public UserNotFoundException(string message) : base(message)
        {
        }

        public UserNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class CommandInvalidException : Exception
    {
        public CommandInvalidException()
        {
        }

        public CommandInvalidException(string message) : base(message)
        {
        }

        public CommandInvalidException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class CommandParameterValidationException : Exception
    {
        public CommandParameterValidationException(string message) : base(message)
        {
        }

        public CommandParameterValidationException()
        {
        }

        public CommandParameterValidationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}