using navigationshurely.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace navigationshurely.services
{
    public class Token
    {
        public TokenType Type { get; init; }

        public string Value { get; init; } = "";

        public int Position { get; init; }

        public int Length { get; init; }
    }
}
