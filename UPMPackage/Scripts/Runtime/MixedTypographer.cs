﻿using System.Linq;

namespace RTLTMPro {
  public class MixedTypographer {
    public static (ContextType[], ContextType) CharactersTypeDetermination(FastStringBuilder input) {
      bool hasArabic = false;
      bool hasEnglish = false;
      ContextType[] isRtl = new ContextType[input.Length];

      #region Mark arabic character

      for (int i = 0; i < input.Length; i++) {
        int ch = input.Get(i);

        bool isArabic = (ch >= '\u0600' && ch <= '\u06FF') ||
                        (ch >= '\u0750' && ch <= '\u077F') ||
                        (ch >= '\u08A0' && ch <= '\u08FF') ||
                        (ch >= '\uFB50' && ch <= '\uFDFF') || //U+FB50 - U+FDFF
                        (ch >= '\uFE70' && ch <= '\uFEFF') || //U+FE70 - U+FEFF
                        (ch >= 0x1EE00 && ch <= 0x1EEFF) ||
                        ch == 0xFFFF;
        isRtl[i] = isArabic ? ContextType.Arabic : ContextType.Default;
        hasArabic = hasArabic || isArabic;
      }

      #endregion

      #region Mark English character

      for (int i = 0; i < isRtl.Length; i++) {
        if (isRtl[i] == 0) {
          int ch = input.Get(i);
          bool isEnglish = Char32Utils.IsEnglishLetter(ch);
          isRtl[i] = isEnglish ? ContextType.English : ContextType.Default;
          hasEnglish = hasEnglish || isEnglish;
        }
      }

      #endregion

      //if no Arabic and English character is find, set to English
      if (!hasArabic && !hasEnglish)
        return (Enumerable.Repeat(ContextType.English, input.Length).ToArray(), ContextType.English);

      #region Mark Punctuation character and symbol character

      for (int i = 0; i < isRtl.Length; i++) {
        if (isRtl[i] == ContextType.Default) {
          int ch = input.Get(i);

          #region Judgment character condition

          ContextType previousType = ContextType.Default;
          ContextType behindType = ContextType.Default;
          // White Space didn't affect text composing in current logic
          // reserve for farther logic optimization
          // bool previousWhiteSpace = false;
          // bool behindWhiteSpace = false;

          // search backward for Context.Type
          // maybe a failed searching 
          for (int j = 1; j <= i; j++) {
            if (isRtl[i - j] != ContextType.Default) {
              previousType = isRtl[i - j];

              break;
            } else if (input.Get(i - j) == ' ') {
              // previousWhiteSpace = true;
            }
          }

          // search forward for Context.Type
          // maybe a failed searching
          for (int j = 1; j + i <= input.Length - 1; j++) {
            if (isRtl[i + j] != ContextType.Default) {
              behindType = isRtl[i + j];
              break;
            } else if (input.Get(i + j) == ' ') {
              // behindWhiteSpace = true;
            }
          }

          if (previousType == ContextType.Default && behindType != ContextType.Default) {
            previousType = behindType;
          } else if (previousType != ContextType.Default && behindType == ContextType.Default) {
            behindType = previousType;
          }

          #endregion

          if (MirroredCharsMaper.MirroredCharsMap.ContainsKey((char)ch)) {
            var mirrorCharacter = MirroredCharsMaper.MirroredCharsMap[(char)ch];
            if (mirrorCharacter > (char)ch) {
              for (int j = 1; j < input.Length - i; j++) {
                if (input.Get(i + j) == mirrorCharacter) {
                  ContextType mirrorPreviousType = ContextType.Default;
                  ContextType mirrorBehindType = ContextType.Default;
                  for (int k = 1; k <= j - 1; k++) {
                    if (isRtl[i + j - k] != ContextType.Default) {
                      mirrorPreviousType = isRtl[i + j - k];
                      break;
                    }
                  }

                  for (int k = 1; k + i + j < input.Length; k++) {
                    if (isRtl[i + j + k] != ContextType.Default) {
                      mirrorBehindType = isRtl[i + j + k];
                      break;
                    }
                  }

                  //if right character is rightest, case previous type only
                  if (i + j == input.Length - 1) behindType = mirrorPreviousType;
                  //if previous type is default, there is no letter in or front this mirror character
                  if (mirrorPreviousType == ContextType.Default) mirrorPreviousType = mirrorBehindType;
                  //if all type is default, all text is not letter
                  if (mirrorPreviousType == ContextType.Default) {
                    isRtl[i] = ContextType.Arabic;
                    isRtl[i + j] = ContextType.Arabic;
                    break;
                  }

                  if (previousType == ContextType.English && behindType == ContextType.English &&
                      mirrorPreviousType == ContextType.English) {
                    isRtl[i] = ContextType.English;
                    isRtl[i + j] = ContextType.English;
                    break;
                  }
                }
              }
            }
          }

          if (isRtl[i] != ContextType.Default) continue;
          if (previousType == ContextType.Arabic || behindType == ContextType.Arabic)
            isRtl[i] = ContextType.Arabic;
          if (previousType == ContextType.English && behindType == ContextType.English)
            isRtl[i] = ContextType.English;
        }
      }

      #endregion

      return (isRtl, hasArabic ? ContextType.Arabic : ContextType.English);
    }
  }
}
