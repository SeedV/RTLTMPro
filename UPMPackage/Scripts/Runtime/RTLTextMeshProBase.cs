using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;

namespace RTLTMPro
{
    public abstract class RTLTextMeshProBase : TextMeshPro
    {
        private readonly Type _unicodeCharType;
        private readonly FieldInfo _unicodeField;
        private readonly FieldInfo _stringIndexField;
        private readonly FieldInfo _lengthField;
        private readonly MethodInfo _methodValidateHtmlTag;

        public List<(int, int)> Tags => _tags;

        protected List<(int, int)> _tags;

        protected RTLTextMeshProBase()
        {
            _unicodeCharType = typeof(TMP_Text).GetNestedType("UnicodeChar",
                BindingFlags.NonPublic | BindingFlags.Instance);
            _unicodeField = _unicodeCharType.GetField("unicode");
            _stringIndexField = _unicodeCharType.GetField("stringIndex");
            _lengthField = _unicodeCharType.GetField("length");
            _methodValidateHtmlTag = typeof(TextMeshProUGUI).GetMethod("ValidateHtmlTag",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public List<(int, int)> FindTags(string input)
        {
            var tags = new List<(int, int)>();
            for (int i = 0; i < input.Length; i++)
            {
                char ch = input[i];
                if (ch == 60)
                {
                    //(char)'<':60
                    // Check if Tag is valid. If valid, skip to the end of the validated tag.
                    if (ValidateTag(input, i + 1, out int endTagIndex))
                    {
                        tags.Add((i, endTagIndex));
                    }
                }
            }

            return tags;
        }

        private bool ValidateTag(string input, int startIndex, out int endIndex)
        {
            if (_methodValidateHtmlTag != null)
            {
                var unicodeChars = Array.CreateInstance(_unicodeCharType, input.Length);
                // 填充数组
                for (int i = 0; i < input.Length; i++)
                {
                    object unicodeChar = Activator.CreateInstance(_unicodeCharType);
                    _unicodeField.SetValue(unicodeChar, input[i]);
                    _stringIndexField.SetValue(unicodeChar, i);
                    _lengthField.SetValue(unicodeChar, 1);
                    unicodeChars.SetValue(unicodeChar, i);
                }

                object[] parameters = { unicodeChars, startIndex, null };
                bool result = (bool)_methodValidateHtmlTag.Invoke(this, parameters);
                endIndex = (int)parameters[2];
                return result;
            }
            else
            {
                endIndex = startIndex;
                return false;
            }
        }
    }
}