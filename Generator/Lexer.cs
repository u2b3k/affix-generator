using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AffixGenerator.Generator
{
    // Token turlari
    public enum TokenType
    {
        SUFFIX, RULE, WHEN, ENDSWITH, STARTSWITH, ISVOWEL, ISCONSONANT, CUT, REPLACE,
        IDENTIFIER, STRING, NUMBER,
        LBRACE, RBRACE, LBRACKET, RBRACKET, LPAREN, RPAREN,
        COMMA, COLON, PLUS, AT, HASH, MINUS, SLASH,
        LITERAL_SET, REGEX_PATTERN, EOF
    }

    // Tokenni saqlash uchun
    public class Token
    {
        public TokenType Type { get; set; }
        public string Value { get; set; } = "";
        public int Line { get; set; }
        public int Column { get; set; }

        public override string ToString() => $"{Type}: {Value} ({Line}:{Column})";
    }

    // Lekser
    public class Lexer
    {

        private readonly string _input;
        private int _position;
        private int _line = 1;
        private int _column = 1;

        // Kalit so'zlar
        private static readonly Dictionary<string, TokenType> Keywords = new()
        {
            { "SUFFIX", TokenType.SUFFIX },
            { "RULE", TokenType.RULE },
            { "WHEN", TokenType.WHEN },
            { "ENDSWITH", TokenType.ENDSWITH },
            { "STARTSWITH", TokenType.STARTSWITH },
            { "ISVOWEL", TokenType.ISVOWEL },
            { "ISCONSONANT", TokenType.ISCONSONANT },
            { "CUT", TokenType.CUT },
            { "REPLACE", TokenType.REPLACE }
        };

        // O'zbek tili uchun unli harflar (kirill yozuvida)
        // Lotin alifbosi uchun o'zgartirish mumkin
        private static readonly char[] Vowels = { 'а', 'е', 'ё', 'и', 'о', 'у', 'ў', 'э', 'ю', 'я' };
        // O'zbek tili uchun undosh harflar (kirill yozuvida)
        // Lotin alifbosi uchun o'zgartirish mumkin
        private static readonly char[] Consonants = { 'б', 'в', 'г', 'д', 'ж', 'з', 'й', 'к', 'л', 'м', 'н', 'п', 'р', 'с', 'т', 'ф', 'х', 'ц', 'ч', 'ш', 'щ', 'ъ', 'ь', 'қ', 'ғ', 'ҳ' };

        // Unli harfmi?
        public static bool IsVowel(char c) => Vowels.Contains(char.ToLower(c));
        // Undosh harfmi?
        public static bool IsConsonant(char c) => Consonants.Contains(char.ToLower(c));

        public Lexer(string input)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
        }

        // Tokenlarga ajratish
        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();

            while (_position < _input.Length)
            {
                SkipWhitespace();

                if (_position >= _input.Length) break;

                var token = NextToken();
                if (token != null)
                    tokens.Add(token);
            }

            tokens.Add(new Token { Type = TokenType.EOF, Line = _line, Column = _column });
            return tokens;
        }

        // Keyingi token
        private Token? NextToken()
        {
            var startLine = _line;
            var startColumn = _column;

            char current = _input[_position];

            // Izohlar
            if (current == '#')
            {
                SkipComment();
                return null;
            }

            // Regex ifodalar
            if (current == '/' && _position + 1 < _input.Length)
            {
                return ReadRegexPattern();
            }

            // Maxsus belgilar
            var singleChar = current switch
            {
                '{' => TokenType.LBRACE,
                '}' => TokenType.RBRACE,
                '[' => TokenType.LBRACKET,
                ']' => TokenType.RBRACKET,
                '(' => TokenType.LPAREN,
                ')' => TokenType.RPAREN,
                ',' => TokenType.COMMA,
                ':' => TokenType.COLON,
                '+' => TokenType.PLUS,
                '@' => TokenType.AT,
                //'-' => TokenType.MINUS,
                _ => (TokenType?)null
            };

            // Joriy belgi maxsus bo'lsa keyingi belgiga o'tish
            if (singleChar.HasValue)
            {
                _position++;
                _column++;
                return new Token { Type = singleChar.Value, Value = current.ToString(), Line = startLine, Column = startColumn };
            }

            // Mantli literallar
            if (current == '"')
            {
                return ReadString();
            }

            // Sonlar
            if (char.IsDigit(current))
            {
                return ReadNumber();
            }

            // Belgilar to'plami, [аиоуё] га ўхшаш
            if (current == '[')
            {
                return ReadLiteralSet();
            }

            // Identifikator va kalit so'zlar
            if (char.IsLetter(current) || current == '_' || current == '-' && _position + 1 < _input.Length && char.IsLetter(_input[_position + 1]))
            {
                return ReadIdentifier();
            }

            throw new Exception($"Notog'ri belgi '{current}', qator nomeri => {_line}:{_column}");
        }

        // Regex ifodani aniqlash
        private Token ReadRegexPattern()
        {
            var startLine = _line;
            var startColumn = _column;
            var sb = new StringBuilder();

            _position++; // Boshidagi '/' belgisini tashlab o'tish
            _column++;

            while (_position < _input.Length && _input[_position] != '/')
            {
                if (_input[_position] == '\\' && _position + 1 < _input.Length)
                {
                    _position++; // Backslash belgisidan keyingi belgiga o'tish
                    _column++;
                    sb.Append('\\'); 
                    sb.Append(_input[_position]);
                }
                else
                {
                    sb.Append(_input[_position]);
                }

                if (_input[_position] == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }

                _position++;
            }

            if (_position >= _input.Length)
                throw new Exception($"Regex ifodasi yopilmagan, qator nomeri => {startLine}:{startColumn}");

            _position++; // Yopuvchi '/' belgisini tashlab o'tish
            _column++;

            return new Token { Type = TokenType.REGEX_PATTERN, Value = sb.ToString(), Line = startLine, Column = startColumn };
        }

        // Matnli literalni aniqlash
        private Token ReadString()
        {
            var startLine = _line;
            var startColumn = _column;
            var sb = new StringBuilder();

            _position++; // Ochuvchi qo'shtirnoqni tashlab o'tish
            _column++;

            while (_position < _input.Length && _input[_position] != '"')
            {
                if (_input[_position] == '\\' && _position + 1 < _input.Length)
                {
                    _position++; 
                    _column++;
                    sb.Append(_input[_position]); 
                }
                else
                {
                    sb.Append(_input[_position]);
                }

                if (_input[_position] == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }

                _position++;
            }

            if (_position >= _input.Length)
                throw new Exception($"Yopilmagan matnli literal, qator nomeri => {startLine}:{startColumn}");

            _position++; // Yopuvchi qo'shtirnoqni tashlab o'tish
            _column++;

            return new Token { Type = TokenType.STRING, Value = sb.ToString(), Line = startLine, Column = startColumn };
        }

        // Sonlarni aniqlash
        private Token ReadNumber()
        {
            var startLine = _line;
            var startColumn = _column;
            var sb = new StringBuilder();

            while (_position < _input.Length && char.IsDigit(_input[_position]))
            {
                sb.Append(_input[_position]);
                _position++;
                _column++;
            }

            return new Token { Type = TokenType.NUMBER, Value = sb.ToString(), Line = startLine, Column = startColumn };
        }

        // Belgilar to'plamini aniqlash
        private Token ReadLiteralSet()
        {
            var startLine = _line;
            var startColumn = _column;
            var sb = new StringBuilder();

            _position++; // Boshidagi '[' ni tashlab o'tish
            _column++;

            while (_position < _input.Length && _input[_position] != ']')
            {
                sb.Append(_input[_position]);
                _position++;
                _column++;
            }

            if (_position >= _input.Length)
                throw new Exception($"Yopilmagan belgilar to'plami, qator nomeri => {startLine}:{startColumn}");

            _position++; // Oxiridagi ']' ni tashlab o'tish
            _column++;

            return new Token { Type = TokenType.LITERAL_SET, Value = sb.ToString(), Line = startLine, Column = startColumn };
        }

        // Identifikator va kalit so'zlarni aniqlash
        private Token ReadIdentifier()
        {
            var startLine = _line;
            var startColumn = _column;
            var sb = new StringBuilder();

            // Chiziqcha bilan boshlanuvchi qo'shimchalar (masalan,  -ю, -у lar) uchun
            if (_input[_position] == '-')
            {
                sb.Append(_input[_position]);
                _position++;
                _column++;
            }

            while (_position < _input.Length &&
                   (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
            {
                sb.Append(_input[_position]);
                _position++;
                _column++;
            }

            var value = sb.ToString();
            var tokenType = Keywords.ContainsKey(value.ToUpper()) ? Keywords[value.ToUpper()] : TokenType.IDENTIFIER;

            return new Token { Type = tokenType, Value = value, Line = startLine, Column = startColumn };
        }

        // Bo'sh joy va yangi qatorlarni tashlab o'tish
        private void SkipWhitespace()
        {
            while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
            {
                if (_input[_position] == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
                _position++;
            }
        }

        // Izohlarni tashlab o'tish
        private void SkipComment()
        {
            while (_position < _input.Length && _input[_position] != '\n')
            {
                _position++;
                _column++;
            }
        }
    }
}
