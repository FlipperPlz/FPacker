using System.Runtime.Serialization;

namespace FPacker.Exceptions; 

public class ParseException : Exception {
    public readonly IEnumerable<string> ParseErrors;

    public ParseException(IEnumerable<string> parseErrors) {
        ParseErrors = parseErrors;
    }
    
}