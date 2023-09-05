﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Variety
{
    public class Wire : Item
    {
        public override string TwitchHelpMessage => "!{0} cut blue [cut a wire]";

        public override void SetColorblind(bool on)
        {
            _wire.GetComponent<Renderer>().material.mainTexture = on ? _prefab.ColorblindTextures[(int) Color] : _prefab.ColorblindTextures[0];
        }

        public Wire(VarietyModule module, WireColor color, int[] cells, Func<KMBombInfo, bool> edgeworkCondition) : base(module, cells)
        {
            Color = color;
            EdgeworkCondition = edgeworkCondition;
        }

        public override bool DecideStates(int numPriorNonWireItems)
        {
            _conditionFlipped = EdgeworkCondition(Module.Bomb);
            SetState(_conditionFlipped ? 1 : 0, automatic: true);
            return true;
        }

        public WireColor Color { get; private set; }
        public Func<KMBombInfo, bool> EdgeworkCondition { get; private set; }

        private bool _isStuck = false;
        private bool _isCut = false;
        private bool _conditionFlipped;
        private KMSelectable _wire;
        private WirePrefab _prefab;

        public override bool IsStuck => _isStuck;
        public override void Checked() { _isStuck = _isCut; }

        public override IEnumerable<ItemSelectable> SetUp(System.Random rnd)
        {
            _prefab = UnityEngine.Object.Instantiate(Module.WireTemplate, Module.transform);
            var seed = rnd.Next(0, int.MaxValue);

            var x1 = GetX(Cells[0]);
            var x2 = GetX(Cells[1]);
            var y1 = GetY(Cells[0]);
            var y2 = GetY(Cells[1]);

            var length = Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
            var numSegments = Math.Max(2, (int) Math.Floor(Math.Sqrt(Math.Pow((Cells[1] % W) - (Cells[0] % W), 2) + Math.Pow((Cells[1] / W) - (Cells[0] / W), 2))));

            _prefab.WireMeshFilter.sharedMesh = WireMeshGenerator.GenerateWire(length, numSegments, WireMeshGenerator.WirePiece.Uncut, highlight: false, seed: seed);
            var hl = WireMeshGenerator.GenerateWire(length, numSegments, WireMeshGenerator.WirePiece.Uncut, highlight: true, seed: seed);
            SetHighlightMesh(_prefab.WireHighlightMeshFilter, hl);
            _prefab.WireCollider.sharedMesh = hl;
            _prefab.WireMeshRenderer.sharedMaterial = _prefab.WireMaterials[(int) Color];

            _prefab.Base1.transform.localPosition = new Vector3(x1, 0.015f, y1);
            _prefab.Base2.transform.localPosition = new Vector3(x2, 0.015f, y2);
            _wire = _prefab.Wire;
            _wire.transform.localPosition = new Vector3(x1, 0.035f, y1);
            _wire.transform.localEulerAngles = new Vector3(0, Mathf.Atan2(y1 - y2, x2 - x1) / Mathf.PI * 180, 0);

            yield return new ItemSelectable(_wire, Cells[0]);

            _wire.OnInteract = delegate
            {
                if (_isCut)
                    return false;
                _isCut = true;

                _wire.AddInteractionPunch(.5f);
                Module.Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSnip, _wire.transform);

                _prefab.WireMeshFilter.sharedMesh = WireMeshGenerator.GenerateWire(length, numSegments, WireMeshGenerator.WirePiece.Cut, highlight: false, seed: seed);
                _prefab.WireCopperMeshFilter.sharedMesh = WireMeshGenerator.GenerateWire(length, numSegments, WireMeshGenerator.WirePiece.Copper, highlight: false, seed: seed);
                var highlightMesh = WireMeshGenerator.GenerateWire(length, numSegments, WireMeshGenerator.WirePiece.Cut, highlight: true, seed: seed);
                SetHighlightMesh(_prefab.WireHighlightMeshFilter, highlightMesh);
                SetState(_conditionFlipped ? 0 : 1);
                return false;
            };
        }

        private void SetHighlightMesh(MeshFilter mf, Mesh highlightMesh)
        {
            mf.sharedMesh = highlightMesh;
            var child = mf.transform.Find("Highlight(Clone)");
            var filter = child?.GetComponent<MeshFilter>();
            if (filter != null)
                filter.sharedMesh = highlightMesh;
        }

        private static readonly string[] _colorNames = { "black", "blue", "red", "yellow", "white" };

        public override string ToString() => $"{_colorNames[(int) Color]} wire";
        public override bool CanProvideStage => false;
        public override int NumStates => 2;
        public override object Flavor => Color;
        public override string DescribeSolutionState(int state) => $"{((state == 0) ^ _conditionFlipped ? "don’t cut" : "cut")} the {_colorNames[(int) Color]} wire";
        public override string DescribeWhatUserDid() => $"you cut the {_colorNames[(int) Color]} wire";
        public override string DescribeWhatUserShouldHaveDone(int desiredState) => $"you {((State == 0) ^ _conditionFlipped ? "should" : "should not")} have cut the {_colorNames[(int) Color]} wire";

        public override IEnumerator ProcessTwitchCommand(string command)
        {
            var m = Regex.Match(command, $@"^\s*cut\s+{_colorNames[(int) Color]}\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!m.Success || _isCut)
                return null;
            return TwitchCut().GetEnumerator();
        }

        private IEnumerable<object> TwitchCut()
        {
            _wire.OnInteract();
            yield return new WaitForSeconds(.1f);
        }

        public override IEnumerable<object> TwitchHandleForcedSolve(int desiredState) => State != (_conditionFlipped ? 0 : 1) ? TwitchCut() : null;
    }
}