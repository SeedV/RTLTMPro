using System.Collections.Generic;

namespace RTLTMPro {
  public static class MixedTypographer {
    private const char _whiteSpace = ' ';

    public static (ContextType[], ContextType) CharactersTypeDetermination(FastStringBuilder input,
      List<(int, int)> tags, bool fixTextTags) {
      bool hasRightToLeft = false;
      bool hasLeftToRight = false;
      var inputCharactersType = new ContextType[input.Length];

      #region Mark Tags

      if (fixTextTags && tags != null) {
        foreach (var (start, end) in tags) {
          for (int j = start; j <= end; j++) {
            inputCharactersType[j] = ContextType.Tag;
          }
        }
      }

      #endregion
      #region Mark RTL character

      for (int i = 0; i < input.Length; i++) {
        if (inputCharactersType[i] != ContextType.Default)
          continue;
        int ch = input.Get(i);
        bool isRightToLeft =
            // \U0600 to \ u06FF: Arabic basic character set (including Arabic letters and some
            // punctuation marks).
            (ch >= '\u0600' && ch <= '\u06FF') ||
            // \U0750 to \ U077F: Includes Arabic characters from some minority languages
            // (such as West African Arabic).
            (ch >= '\u0750' && ch <= '\u077F') ||
            // \U08A0 to \ u08FF: Contains extended Arabic characters
            // (such as Arabic diacritical marks).
            (ch >= '\u08A0' && ch <= '\u08FF') ||
            // \UFB50 to \ uFDFF: Arabic glyph variants
            // (such as connected letters).
            (ch >= '\uFB50' && ch <= '\uFDFF') ||
            // \UFE70 to \ uFEFF: Special marks and variant characters in Arabic.
            (ch >= '\uFE70' && ch <= '\uFEFF') ||
            // 0x1EE00 to 0x1EEFF: including variations of Latin letters
            // and some additional Arabic characters.
            (ch >= 0x1EE00 && ch <= 0x1EEFF) ||
            // 0xFFFF: A special character that typically represents an undefined character
            // or placeholder.
            ch == 0xFFFF;
        inputCharactersType[i] = isRightToLeft ? ContextType.RightToLeft : ContextType.Default;
        hasRightToLeft = hasRightToLeft || isRightToLeft;
      }

      #endregion

      #region Mark LTR character

      for (int i = 0; i < inputCharactersType.Length; i++) {
        if (inputCharactersType[i] != ContextType.Default) continue;
        if (inputCharactersType[i] == 0) {
          int ch = input.Get(i);
          bool isLeftToRight = Char32Utils.IsLetter(ch) && !Char32Utils.IsRTLCharacter(ch);
          inputCharactersType[i] = isLeftToRight ? ContextType.LeftToRight : ContextType.Default;
          hasLeftToRight = hasLeftToRight || isLeftToRight;
        }
      }

      #endregion

      // If no RightToLeft and LeftToRight character is found, set to LeftToRight
      if (!hasRightToLeft && !hasLeftToRight) {
        for (int j = 0; j < inputCharactersType.Length; j++) {
          if (inputCharactersType[j] == ContextType.Default) {
            inputCharactersType[j] = ContextType.LeftToRight;
          }
        }

        return (inputCharactersType, ContextType.LeftToRight);
      }

      #region Mark Punctuation character and symbol character

      for (int i = 0; i < inputCharactersType.Length; i++) {
        if (inputCharactersType[i] == ContextType.Default) {
          int ch = input.Get(i);
          #region Mark Mirrored Character

          if (MirroredCharsMaper.MirroredCharsMap.ContainsKey((char)ch)) {
            GetMirroredCharsType(input, i, inputCharactersType);
          }

          #endregion

          #region Mark Paired Character

          if (PairedCharsMaper.PairedCharsSet.Contains((char)ch)) {
            #region Judgment character condition

            ContextType previousType = ContextType.Default;
            ContextType behindType = ContextType.Default;

            (previousType, _, behindType, _) =
                GetNearbyType(input, i, inputCharactersType);

            #endregion
            for (int j = 1; j < input.Length - i; j++) {
              if (input.Get(i + j) == ch) {
                ContextType middleType = ContextType.Default;
                ContextType pairedPreviousType = ContextType.Default;
                ContextType pairedBehindType = ContextType.Default;
                for (int k = 1; k <= j - 1; k++) {
                  if (pairedPreviousType == ContextType.Default &&
                      inputCharactersType[i + j - k] != ContextType.Default &&
                      inputCharactersType[i + j - k] != ContextType.Tag) {
                    pairedPreviousType = inputCharactersType[i + j - k];
                  }

                  if (middleType == ContextType.Default &&
                      inputCharactersType[i + j - k] == ContextType.LeftToRight) {
                    middleType = ContextType.LeftToRight;
                  }

                  if (inputCharactersType[i + j - k] == ContextType.RightToLeft) {
                    middleType = ContextType.RightToLeft;
                  }
                }

                for (int k = 1; k + i + j < input.Length; k++) {
                  if (inputCharactersType[i + j + k] != ContextType.Default &&
                      inputCharactersType[i + j + k] != ContextType.Tag) {
                    pairedBehindType = inputCharactersType[i + j + k];
                    break;
                  }
                }

                // If right character is rightest, case previous type only
                if (i + j == input.Length - 1) pairedBehindType = pairedPreviousType;
                // If previous type is default, there is no letter in or front this mirror character
                if (pairedPreviousType == ContextType.Default)
                  pairedPreviousType = pairedBehindType;
                // If all type is default, all text is not letter
                if (pairedPreviousType == ContextType.Default &&
                    previousType == ContextType.Default) {
                  inputCharactersType[i] = ContextType.LeftToRight;
                  inputCharactersType[i + j] = ContextType.LeftToRight;
                }
                if (pairedPreviousType == ContextType.Default &&
                    previousType != ContextType.Default) {
                  inputCharactersType[i] = previousType;
                  inputCharactersType[i + j] = previousType;
                  break;
                }
                if (previousType == ContextType.LeftToRight
                    && middleType == ContextType.LeftToRight
                    && behindType == ContextType.LeftToRight
                    && pairedPreviousType == ContextType.LeftToRight) {
                  inputCharactersType[i] = ContextType.LeftToRight;
                  inputCharactersType[i + j] = ContextType.LeftToRight;
                  break;
                }
                inputCharactersType[i] = ContextType.RightToLeft;
                inputCharactersType[i + j] = ContextType.RightToLeft;
                break;
              }
            }
          }

          #endregion

          #region Mark Normal Punctuation character and symbol character

          if (inputCharactersType[i] != ContextType.Default) continue;
          else {
             #region Judgment character condition

             ContextType previousType = ContextType.Default;
             ContextType behindType = ContextType.Default;

             bool previousWhiteSpace = false;
             bool behindWhiteSpace = false;

             (previousType, previousWhiteSpace, behindType, behindWhiteSpace) =
                 GetNearbyType(input, i, inputCharactersType);

             #endregion

             if (previousType == ContextType.LeftToRight &&
                 behindType == ContextType.LeftToRight) {
               inputCharactersType[i] = ContextType.LeftToRight;
               continue;
             }

             if (previousType == ContextType.RightToLeft &&
                 behindType == ContextType.RightToLeft) {
               inputCharactersType[i] = ContextType.RightToLeft;
               continue;
             }

             // If this character is a white space, previous & behind are not LeftToRight same time
             if (input.Get(i) == _whiteSpace) {
               inputCharactersType[i] = ContextType.RightToLeft;
             }

             // In order to clearly see the judgment logic, ignore the grammar prompts here
             if (previousWhiteSpace == false && behindWhiteSpace == true) {
               inputCharactersType[i] = previousType;
               continue;
             }

             // In order to clearly see the judgment logic, ignore the grammar prompts here
             if (previousWhiteSpace == true && behindWhiteSpace == false) {
               inputCharactersType[i] = behindType;
               continue;
             }

             // In order to clearly see the judgment logic, ignore the grammar prompts here
             if (previousWhiteSpace == false && behindWhiteSpace == false) {
               inputCharactersType[i] = ContextType.RightToLeft;
             }

             // In order to clearly see the judgment logic, ignore the grammar prompts here
             if (previousWhiteSpace == true && behindWhiteSpace == true) {
               inputCharactersType[i] = ContextType.RightToLeft;
             }
          }

          #endregion
        }
      }

      #endregion

      return (inputCharactersType,
        hasRightToLeft ? ContextType.RightToLeft : ContextType.LeftToRight);
    }
    
    /// <summary>
    /// GetMirroredCharsType use for set context type and return valid mirrored character index
    /// </summary>
    /// <param name="input">input string</param>
    /// <param name="index">start mirror character index</param>
    /// <param name="inputCharactersType">array for every character's context type</param>
    /// <returns>mirrored character index</returns>
    private static int GetMirroredCharsType(FastStringBuilder input, int index, 
        ContextType[] inputCharactersType) {
      int ch = input.Get(index);
      int mirroredCharacterIndex = index;
      if (!MirroredCharsMaper.MirroredCharsMap.ContainsKey((char)ch)) return mirroredCharacterIndex;
      if (inputCharactersType[index] == ContextType.Tag) {
        for (int j = 1; j < input.Length - index; j++) {
          if (inputCharactersType[index + j] == ContextType.Tag) {
            mirroredCharacterIndex = index + j;
          }

          if (inputCharactersType.Length - 1 == index + j ||
              inputCharactersType[index + j] != ContextType.Tag) {
            return mirroredCharacterIndex;
          }
        }
      }
      #region Judgment character condition

      ContextType previousType = ContextType.Default;

      (previousType, _, _, _) = GetNearbyType(input, index, inputCharactersType);

      #endregion
      var mirrorCharacter = MirroredCharsMaper.MirroredCharsMap[(char)ch];
      if (mirrorCharacter > (char)ch) {

        for (int j = 1; j < input.Length - index; j++) {
          if (input.Get(index + j) == mirrorCharacter) {
            mirroredCharacterIndex = index + j;
            ContextType mirrorPreviousType = ContextType.Default;
            ContextType mirrorBehindType = ContextType.Default;
            ContextType middleContentType = ContextType.Default;
            for (int k = 1; k <= j - 1; k++) {
              ContextType typeThere = inputCharactersType[index + j - k];
              if (mirrorPreviousType == ContextType.Default &&
                  typeThere != ContextType.Default && typeThere != ContextType.Tag) {
                mirrorPreviousType = inputCharactersType[index + j - k];
              }

              if (middleContentType == ContextType.Default &&
                  typeThere == ContextType.LeftToRight) {
                middleContentType = ContextType.LeftToRight;
              }

              if (typeThere == ContextType.RightToLeft) {
                middleContentType = ContextType.RightToLeft;
              }
            }

            for (int k = 1; k + index + j < input.Length; k++) {
              if (inputCharactersType[index + j + k] != ContextType.Default &&
                  inputCharactersType[index + j + k] != ContextType.Tag) {
                mirrorBehindType = inputCharactersType[index + j + k];
                break;
              }
            }

            // If the current character is the last in the input,
            // only consider the previous type.
            if (index + j == input.Length - 1) {
              mirrorBehindType = mirrorPreviousType;
            }

            // If the previous type is default,
            // assume no letter exists before this character.
            if (mirrorPreviousType == ContextType.Default) {
              mirrorPreviousType = mirrorBehindType;
            }

            // If both previous and next types are default,
            // assume the text contains no letters.
            if (mirrorPreviousType == ContextType.Default) {
              inputCharactersType[index] = previousType;
              inputCharactersType[index + j] = previousType;
              break;
            }

            if (middleContentType == ContextType.LeftToRight 
                || middleContentType == ContextType.Default 
                && previousType == ContextType.LeftToRight 
                && mirrorBehindType == ContextType.LeftToRight) {
              inputCharactersType[index] = ContextType.LeftToRight;
              inputCharactersType[index + j] = ContextType.LeftToRight;
            } else {
              inputCharactersType[index] = ContextType.RightToLeft;
              inputCharactersType[index + j] = ContextType.RightToLeft;
            }

            break;
          }

          if (input.Get(index + j) == ch) {
            int childEndIndex = GetMirroredCharsType(input, index + j, inputCharactersType);
            j = childEndIndex - index;
          }
        }
      }
      return mirroredCharacterIndex;
    }

    private static (ContextType, bool, ContextType, bool) GetNearbyType(
      FastStringBuilder input, int index, ContextType[] inputCharactersType) {
      ContextType previousType = ContextType.Default;
      ContextType behindType = ContextType.Default;

      bool previousWhiteSpace = false;
      bool behindWhiteSpace = false;

      // Search forward for Context.Type.
      // Maybe a failed searching.
      for (int j = 1; j <= index; j++) {
        if (input.Get(index - j) == _whiteSpace) {
          previousWhiteSpace = true;
        }

        if (inputCharactersType[index - j] != ContextType.Default &&
            inputCharactersType[index - j] != ContextType.Tag) {
          previousType = inputCharactersType[index - j];

          break;
        }
      }

      // Search forward for Context.Type.
      // Maybe a failed searching.
      for (int j = 1; j + index <= input.Length - 1; j++) {
        if (input.Get(index + j) == _whiteSpace) {
          behindWhiteSpace = true;
        }

        if (inputCharactersType[index + j] != ContextType.Default &&
            inputCharactersType[index + j] != ContextType.Tag) {
          behindType = inputCharactersType[index + j];
          break;
        }
      }

      if (previousType == ContextType.Default && behindType != ContextType.Default) {
        previousType = behindType;
      } else if (previousType != ContextType.Default && behindType == ContextType.Default) {
        behindType = previousType;
      }

      return (previousType, previousWhiteSpace, behindType, behindWhiteSpace);
    }
  }
}
