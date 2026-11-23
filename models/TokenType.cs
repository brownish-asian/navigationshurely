using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace navigationshurely.models
{
    public enum TokenType
    {
        Word,
        String,

        Semicolon,
        LeftBrace,
        RightBrace,
        LeftBracket,
        RightBracket,
        Equals,
        Comma,
        FullStop,
        LeftSquareBracket,
        RightSquareBracket,
        Hyphen,
        Plus,
        Colon,

        EndOfInput
    }
}
