using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedKernel
{
    public class Error
    {
        public string Code { get; }
        public ErrorType Type { get; }
        public string Message { get; }
        public Error(string code, string message, ErrorType type)
        {
            Code = code;
            Type = type;
            Message = message;
        }

        public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

        public static readonly Error NullValue = new(
            "General.Null", 
            "Null value was provided", 
            ErrorType.NullValue);

        public static Error Failure(string code, string message) =>
            new(code, message, ErrorType.Faulure);

        public static Error Validation(string code, string message) =>
            new(code, message, ErrorType.Validation);

        public static Error Problem(string code, string message) =>
            new(code, message, ErrorType.Problem);

        public static Error NotFound(string code, string message) =>
            new(code, message, ErrorType.NotFound);

        public static Error Conflict(string code, string message) =>
            new(code, message, ErrorType.Conflict);
    }
}
