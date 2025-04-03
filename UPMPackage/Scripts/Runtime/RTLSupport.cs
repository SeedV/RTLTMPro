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
        /// <param name="redirection">Redirection array</param>
        /// <param name="fixTextTags"></param>
        /// <param name="preserveNumbers"></param>
        /// <param name="farsi"></param>
        /// <returns>Fixed text</returns>
        public static void FixRTL(
            string input,
            RTLTextMeshProBase textMeshPro,
            FastStringBuilder output,
            out int[] redirection,
            bool farsi = true,
            bool fixTextTags = true,
            bool preserveNumbers = false)
        {
            inputBuilder.SetValue(input);
            redirection = new int[inputBuilder.Length];
            for (int i = 0; i < inputBuilder.Length; i++)
            {
                redirection[i] = i;
            }
            TashkeelFixer.RemoveTashkeel(inputBuilder, redirection);
            // The shape of the letters in shapeFixedLetters is fixed according to their position in word. But the flow of the text is not fixed.
            // Letters count is the same as the input.
            GlyphFixer.Fix(inputBuilder, glyphFixerOutput, preserveNumbers, farsi, fixTextTags);
            //Restore tashkeel to their places.
            TashkeelFixer.RestoreTashkeel(glyphFixerOutput, redirection);

            TashkeelFixer.FixShaddaCombinations(glyphFixerOutput, redirection);
            // Fix flow of the text and put the result in FinalLetters field
            LigatureFixer.Fix(glyphFixerOutput, redirection, textMeshPro.Tags, output, farsi, fixTextTags,
                preserveNumbers);

            inputBuilder.Clear();
        }
    }
}