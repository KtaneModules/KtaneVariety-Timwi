﻿using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Variety
{
    public class Slider : Item
    {
        public override string TwitchHelpMessage => "!{0} horiz/vert 0 [set horizontal/vertical slider]";

        public const int LongSlots = 5;
        public const int ShortSlots = 2;

        public int TopLeftCell { get; private set; }
        public SliderOrientation Orientation { get; private set; }
        public int NumTicks { get; private set; }

        public static int SW(SliderOrientation orientation) => orientation == SliderOrientation.HorizontalSlider ? LongSlots : ShortSlots;
        public static int SH(SliderOrientation orientation) => orientation == SliderOrientation.HorizontalSlider ? ShortSlots : LongSlots;

        private static readonly string[] _orientationNames = { "horizontal", "vertical" };
        private SliderPrefab _prefab;
        private Coroutine _sliderMoving;

        public Slider(VarietyModule module, int topLeftCell, SliderOrientation orientation, System.Random rnd) : base(module, CellRect(topLeftCell, SW(orientation), SH(orientation)))
        {
            TopLeftCell = topLeftCell;
            Orientation = orientation;
            NumTicks = rnd.Next(3, 8);
        }

        public override IEnumerable<ItemSelectable> SetUp(System.Random rnd)
        {
            _prefab = Object.Instantiate(Module.SliderTemplate, Module.transform);

            if (Orientation == SliderOrientation.HorizontalSlider)
            {
                var x = -VarietyModule.Width / 2 + (TopLeftCell % W + (LongSlots - 1) * .5f) * VarietyModule.CellWidth;
                var y = VarietyModule.Height / 2 - (TopLeftCell / W + (ShortSlots - 1) * .5f) * VarietyModule.CellHeight + VarietyModule.YOffset;
                _prefab.transform.localPosition = new Vector3(x, 0, y);
                _prefab.transform.localRotation = Quaternion.identity;
            }
            else
            {
                var x = -VarietyModule.Width / 2 + (TopLeftCell % W + (ShortSlots - 1) * .5f) * VarietyModule.CellWidth;
                var y = VarietyModule.Height / 2 - (TopLeftCell / W + (LongSlots - 1) * .5f) * VarietyModule.CellHeight + VarietyModule.YOffset;
                _prefab.transform.localPosition = new Vector3(x, 0, y);
                _prefab.transform.localRotation = Quaternion.Euler(0, 90, 0);
            }
            _prefab.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);

            SetState(rnd.Next(0, NumTicks), automatic: true);
            _prefab.Knob.transform.localPosition = new Vector3(XPosition(State), .021f, 0);

            for (var tick = 0; tick < NumTicks; tick++)
            {
                var obj = tick == 0 ? _prefab.TickTemplate : Object.Instantiate(_prefab.TickTemplate, _prefab.transform);
                obj.transform.localPosition = new Vector3(XPosition(tick), .0151f, .0075f);
                obj.transform.localRotation = Quaternion.Euler(90, 0, 0);
                obj.transform.localScale = new Vector3(.001f, .005f, 1);
            }

            var movingRight = State != NumTicks - 1;

            _prefab.Knob.OnInteract += delegate
            {
                _prefab.Knob.AddInteractionPunch(.5f);
                Module.Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, _prefab.Knob.transform);

                SetState(State + (movingRight ? 1 : -1));
                if (State == 0)
                    movingRight = true;
                else if (State == NumTicks - 1)
                    movingRight = false;

                if (_sliderMoving != null)
                    Module.StopCoroutine(_sliderMoving);
                _sliderMoving = Module.StartCoroutine(MoveKnob(_prefab.Knob.transform.localPosition.x, XPosition(State), _prefab.Knob.transform));
                return false;
            };

            yield return new ItemSelectable(_prefab.Knob, TopLeftCell % W + SW(Orientation) / 2 + W * (TopLeftCell / W + SH(Orientation) / 2));
        }

        private IEnumerator MoveKnob(float startX, float endX, Transform knob)
        {
            var duration = .2f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                knob.transform.localPosition = new Vector3(Easing.InOutQuad(elapsed, startX, endX, duration), .021f, 0);
                yield return null;
                elapsed += Time.deltaTime;
            }
            knob.transform.localPosition = new Vector3(endX, .021f, 0);
            _sliderMoving = null;
        }

        private float XPosition(int state) => Mathf.Lerp(-0.028f, 0.028f, state * 1f / (NumTicks - 1));

        public override string ToString() => $"{_orientationNames[(int) Orientation]} slider";
        public override int NumStates => NumTicks;
        public override object Flavor => Orientation;

        public override string DescribeSolutionState(int state) => $"set the {_orientationNames[(int) Orientation]} slider to {state}";
        public override string DescribeWhatUserDid() => $"you changed the {_orientationNames[(int) Orientation]} slider";
        public override string DescribeWhatUserShouldHaveDone(int desiredState) => $"you should have set the {_orientationNames[(int) Orientation]} slider to {desiredState} (instead of {State})";

        public override IEnumerator ProcessTwitchCommand(string command)
        {
            var m = Regex.Match(command, $@"^\s*{(Orientation == SliderOrientation.HorizontalSlider ? "horiz" : "vert")}\s+(\d+)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            int val;
            if (m.Success && int.TryParse(m.Groups[1].Value, out val) && val >= 0 && val < NumTicks)
                return TwitchSetTo(val).GetEnumerator();
            return null;
        }

        public override IEnumerable<object> TwitchHandleForcedSolve(int desiredState) => TwitchSetTo(desiredState);

        private IEnumerable<object> TwitchSetTo(int val)
        {
            while (State != val)
            {
                _prefab.Knob.OnInteract();
                while (_sliderMoving != null)
                    yield return null;
                yield return new WaitForSeconds(.1f);
            }
        }
    }
}