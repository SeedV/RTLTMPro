using System;
using TMPro;
using UnityEngine;

namespace RTLTMPro
{
    [ExecuteInEditMode]
    public class RTLTextMeshPro : RTLTextMeshProBase
    {
#if TMP_VERSION_2_1_0_OR_NEWER
        public override string text
#else
        public new string text
#endif
        {
            get { return base.text; }
            set
            {
                if (originalText == value)
                    return;

                originalText = value;

                UpdateText();
            }
        }

        public string OriginalText
        {
            get { return originalText; }
        }

        public int[] ReDirection
        {
            get
            {
                if (_reDirection == null)
                {
                    _reDirection = new int[originalText.Length];
                    for (int i = 0; i < originalText.Length; i++)
                    {
                        _reDirection[i] = i;
                    }
                }

                return _reDirection;
            }
            set => _reDirection = value;
        }

        public bool PreserveNumbers
        {
            get { return preserveNumbers; }
            set
            {
                if (preserveNumbers == value)
                    return;

                preserveNumbers = value;
                havePropertiesChanged = true;
            }
        }

        public bool Farsi
        {
            get { return farsi; }
            set
            {
                if (farsi == value)
                    return;

                farsi = value;
                havePropertiesChanged = true;
            }
        }

        public bool FixTags
        {
            get { return fixTags; }
            set
            {
                if (fixTags == value)
                    return;

                fixTags = value;
                havePropertiesChanged = true;
            }
        }

        public bool ForceFix
        {
            get { return forceFix; }
            set
            {
                if (forceFix == value)
                    return;

                forceFix = value;
                havePropertiesChanged = true;
            }
        }

        [SerializeField] protected bool preserveNumbers;

        [SerializeField] protected bool farsi = true;

        [SerializeField] [TextArea(3, 10)] protected string originalText;

        [SerializeField] protected bool fixTags = true;

        [SerializeField] protected bool forceFix;

        protected readonly FastStringBuilder _finalText =
            new FastStringBuilder(RTLSupport.DefaultBufferSize);

        private int[] _reDirection;

        protected void Update()
        {
            if (havePropertiesChanged)
            {
                UpdateText();
            }
        }

        public void UpdateText()
        {
            if (originalText == null)
                originalText = "";

            _tags = FindTags(originalText);

            if (ForceFix == false && TextUtils.IsRTLInput(originalText) == false)
            {
                isRightToLeftText = false;
                base.text = originalText;
            }
            else
            {
                isRightToLeftText = true;
                (base.text, _reDirection) = GetFixedText(originalText);
            }

            havePropertiesChanged = true;
        }

        private (string, int[]) GetFixedText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return (input, Array.Empty<int>());

            _finalText.Clear();
            RTLSupport.FixRTL(input, this, _finalText, out int[] reDirection, farsi, fixTags,
                preserveNumbers);
            return (_finalText.ToString(), reDirection);
        }
    }
}