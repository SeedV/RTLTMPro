using System.Collections.Generic;

namespace RTLTMPro
{
    public static class RTLSupport
    {
        public const int DefaultBufferSize = 2048;

        private static FastStringBuilder inputBuilder;
        private static FastStringBuilder glyphFixerOutput;

        static RTLSupport()
        {
            inputBuilder = new FastStringBuilder(DefaultBufferSize);
            glyphFixerOutput = new FastStringBuilder(DefaultBufferSize);
        }

        /// <summary>
        ///     Fixes the provided string
        /// </summary>
        /// <param name="input">Text to fix</param>
        /// <param name="textMeshPro">RTLTextMeshPro for find tags</param>
        /// <param name="output">Fixed text</param>
        /// <param name="reDirection">ReDirection array</param>
        /// <param name="fixTextTags"></param>
        /// <param name="preserveNumbers"></param>
        /// <param name="farsi"></param>
        /// <returns>Fixed text</returns>
        public static void FixRTL(
            string input,
            RTLTextMeshPro textMeshPro,
            FastStringBuilder output,
            out int[] reDirection,
            bool farsi = true,
            bool fixTextTags = true,
            bool preserveNumbers = false)
        {
            reDirection = new int[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                reDirection[i] = i;
            }

            inputBuilder.SetValue(input);
            TashkeelFixer.RemoveTashkeel(inputBuilder, reDirection);
            // The shape of the letters in shapeFixedLetters is fixed according to their position in word. But the flow of the text is not fixed.
            // Letters count is the same as the input.
            GlyphFixer.Fix(inputBuilder, glyphFixerOutput, preserveNumbers, farsi, fixTextTags);
            //Restore tashkeel to their places.
            TashkeelFixer.RestoreTashkeel(glyphFixerOutput, reDirection);

            TashkeelFixer.FixShaddaCombinations(glyphFixerOutput, reDirection);
            // Fix flow of the text and put the result in FinalLetters field

            var tags = new List<(int, int)>();
            if (textMeshPro != null)
                tags = textMeshPro.FindTags(glyphFixerOutput.ToString());
            LigatureFixer.Fix(glyphFixerOutput, reDirection, tags, output, farsi, fixTextTags,
                preserveNumbers);
            inputBuilder.Clear();
        }
    }
}