namespace LikeMe
{
    public static class VMExtensions
    {
        public class VMString
        {
            private string _text;

            public VMString(string text)
            {
                _text = text;
            }

            public string SubString(int index)
            {
                return _text.Substring(index);
            }
        }
        public class VMInt
        {
            private int _number;

            public VMInt(int number)
            {
                _number = number;
            }

            public string ToString()
            {
                return _number.ToString();
            }
        }

        public static VMString VM(this string text)
        {
            return new VMString(text);
        }
        public static VMInt VM(this int number)
        {
            return new VMInt(number);
        }
    }
}
