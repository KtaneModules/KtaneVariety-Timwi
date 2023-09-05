﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Variety
{
    public class Maze : Item
    {
        public override string TwitchHelpMessage => "!{0} 3x3 maze UDLR [make moves in the 3×3 maze]";

        private GameObject _colorblindText;
        public override void SetColorblind(bool on)
        {
            _colorblindText.SetActive(on);
        }

        public Maze(VarietyModule module, int x, int y, int width, int height, int startPos, int shape, MazeLayout maze) : base(module, Enumerable.Range(0, width * height).Select(ix => x + ix % width + W * (y + ix / width)).ToArray())
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Shape = shape;
            SetState(startPos, automatic: true);
            _maze = maze;
        }

        public int X { get; private set; }
        public int Y { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Shape { get; private set; }
        public override int NumStates => Width * Height;

        private readonly MazeLayout _maze;
        private KMSelectable[] _buttons;    // up, right, down, left

        private Vector3 Pos(int cell, bool dot = false) => new Vector3((cell % Width) - (Width - 1) * .5f, dot ? .003f : .004f, (Height - 1) * .5f - (cell / Width));

        public override IEnumerable<ItemSelectable> SetUp(System.Random rnd)
        {
            var prefab = UnityEngine.Object.Instantiate(Module.MazeTemplate, Module.transform);
            var cbtext = prefab.GetComponentInChildren<TextMesh>();
            cbtext.text = _symbolColors[Shape % 3][0].ToString().ToUpperInvariant();
            _colorblindText = cbtext.gameObject;

            var cx = -VarietyModule.Width / 2 + (X + Width * .5f) * VarietyModule.CellWidth;
            var cy = VarietyModule.Height / 2 - (Y + Height * .5f) * VarietyModule.CellHeight + VarietyModule.YOffset;
            prefab.transform.localPosition = new Vector3(cx, .01502f, cy);
            prefab.transform.localRotation = Quaternion.identity;
            prefab.transform.localScale = new Vector3(VarietyModule.CellWidth * .75f, VarietyModule.CellWidth * .75f, VarietyModule.CellWidth * .75f);

            var dots = new GameObject[Width * Height];
            for (var dx = 0; dx < Width; dx++)
                for (var dy = 0; dy < Height; dy++)
                {
                    var dot = dx == 0 && dy == 0 ? prefab.Dot : UnityEngine.Object.Instantiate(prefab.Dot, prefab.transform);
                    dot.transform.localPosition = Pos(dx + Width * dy, dot: true);
                    dot.transform.localEulerAngles = new Vector3(90, 0, 0);
                    dot.transform.localScale = new Vector3(.3f, .3f, .3f);
                    dot.SetActive(dx + Width * dy != State);
                    dots[dx + Width * dy] = dot;
                }

            prefab.Position.transform.localPosition = Pos(State);
            prefab.Position.transform.localScale = new Vector3(1, 1, 1);
            prefab.PositionRenderer.material.mainTexture = prefab.PositionTextures[Shape];

            var frameMeshName = $"Frame{Width}x{Height}";
            prefab.Frame.sharedMesh = prefab.FrameMeshes.First(m => m.name == frameMeshName);
            var backMeshName = $"Back{Width}x{Height}";
            prefab.Back.sharedMesh = prefab.BackMeshes.First(m => m.name == backMeshName);

            prefab.ButtonPos[0].localPosition = new Vector3(0, .0001f, .5f + .5f * Height);
            prefab.ButtonPos[1].localPosition = new Vector3(.5f + .5f * Width, .0001f, 0);
            prefab.ButtonPos[2].localPosition = new Vector3(0, .0001f, -.5f - .5f * Height);
            prefab.ButtonPos[3].localPosition = new Vector3(-.5f - .5f * Width, .0001f, 0);

            yield return new ItemSelectable(prefab.Buttons[0], X + Width / 2 + W * Y);
            yield return new ItemSelectable(prefab.Buttons[1], X + Width - 1 + W * (Y + Height / 2));
            yield return new ItemSelectable(prefab.Buttons[2], X + Width / 2 + W * (Y + Height - 1));
            yield return new ItemSelectable(prefab.Buttons[3], X + W * (Y + Height / 2));

            for (var i = 0; i < 4; i++)
                prefab.Buttons[i].OnInteract = ButtonPress(prefab.Buttons[i], i, prefab.Position, dots);
            Module.StartCoroutine(Spin(prefab.Position));
            _buttons = prefab.Buttons;
        }

        private IEnumerator Spin(GameObject position)
        {
            var angle = 0f;
            while (true)
            {
                position.transform.localEulerAngles = new Vector3(90, angle, 0);
                yield return null;
                angle += 15 * Time.deltaTime;
            }
        }

        private static readonly int[] _dxs = { 0, 1, 0, -1 };
        private static readonly int[] _dys = { -1, 0, 1, 0 };

        private KMSelectable.OnInteractHandler ButtonPress(KMSelectable button, int btnIx, GameObject position, GameObject[] dots) => delegate
        {
            button.AddInteractionPunch(.25f);
            Module.Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, button.transform);
            Module.MoveButton(button.transform, .1f, ButtonMoveType.DownThenUp);

            var x = State % Width;
            var y = State / Width;
            var nx = x + _dxs[btnIx];
            var ny = y + _dys[btnIx];

            if (nx < 0 || nx >= Width || ny < 0 || ny >= Height || !_maze.CanGo(State, btnIx))
            {
                Module.Module.HandleStrike();
                Debug.LogFormat(
                    nx < 0 ? @"[Variety #{0}] In the {1}×{2} maze, you tried to go left from {3}{4}, hitting the edge of the maze." :
                    nx >= Width ? @"[Variety #{0}] In the {1}×{2} maze, you tried to go right from {3}{4}, hitting the edge of the maze." :
                    ny < 0 ? @"[Variety #{0}] In the {1}×{2} maze, you tried to go up from {3}{4}, hitting the edge of the maze." :
                    ny >= Height ? @"[Variety #{0}] In the {1}×{2} maze, you tried to go down from {3}{4}, hitting the edge of the maze." :
                    @"[Variety #{0}] In the {1}×{2} maze, you tried to go from {3}{4} to {5}{6} but there’s a wall there.",
                    Module.ModuleID, Width, Height, (char) ('A' + x), y + 1, (char) ('A' + nx), ny + 1);
                return false;
            }

            SetState(nx + Width * ny);
            position.transform.localPosition = Pos(State);
            foreach (var dot in dots)
                dot.SetActive(true);
            dots[State].SetActive(false);
            return false;
        };

        private static readonly string[] _symbolColors = { "red", "yellow", "blue" };
        private static readonly string[] _symbolNames = { "plus", "star", "triangle" };

        public override string ToString() => $"{Width}×{Height} maze with a {_symbolColors[Shape % 3]} {_symbolNames[Shape / 3]}";
        public override object Flavor => $"Maze:{Width}:{Height}";
        public override string DescribeSolutionState(int state) => $"go to {(char) (state % Width + 'A')}{state / Width + 1} in the {Width}×{Height} maze";
        public override string DescribeWhatUserDid() => $"you moved in the {Width}×{Height} maze";
        public override string DescribeWhatUserShouldHaveDone(int desiredState) => $"you should have moved to {(char) (desiredState % Width + 'A')}{desiredState / Width + 1} in the {Width}×{Height} maze (instead of {(char) (State % Width + 'A')}{State / Width + 1})";

        public override IEnumerator ProcessTwitchCommand(string command)
        {
            var m = Regex.Match(command, $@"^\s*{Width}[x×]{Height}\s+maze\s+([udlr]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (m.Success)
                return TwitchMove(m.Groups[1].Value.ToLowerInvariant()).GetEnumerator();
            return null;
        }

        public override IEnumerable<object> TwitchHandleForcedSolve(int desiredState)
        {
            if (State == desiredState)
                return Enumerable.Empty<object>();

            var visited = new Dictionary<int, int>();
            var q = new Queue<int>();
            q.Enqueue(State);

            while (q.Count > 0)
            {
                var item = q.Dequeue();
                var adjs = new List<int>();
                if (_maze.CanGoLeft(item))
                    adjs.Add(item - 1);
                if (_maze.CanGoRight(item))
                    adjs.Add(item + 1);
                if (_maze.CanGoUp(item))
                    adjs.Add(item - Width);
                if (_maze.CanGoDown(item))
                    adjs.Add(item + Width);
                foreach (var adj in adjs)
                {
                    if (adj != State && !visited.ContainsKey(adj))
                    {
                        visited[adj] = item;
                        if (adj == desiredState)
                            goto done;
                        q.Enqueue(adj);
                    }
                }
            }
            done:
            var moves = new List<char>();
            var curPos = desiredState;
            var iter = 0;
            while (visited.ContainsKey(curPos))
            {
                iter++;
                if (iter > 100)
                {
                    Debug.LogFormat("<> State = {0}", State);
                    Debug.LogFormat("<> desiredState = {0}", desiredState);
                    Debug.LogFormat("<> moves = {0}", moves.Join(","));
                    Debug.LogFormat("<> visited:\n{0}", visited.Select(kvp => $"{kvp.Key} <= {kvp.Value}").Join("\n"));
                    throw new InvalidOperationException();
                }

                var newPos = visited[curPos];
                moves.Add(newPos == curPos + 1 ? 'l' : newPos == curPos - 1 ? 'r' : newPos == curPos + Width ? 'u' : 'd');
                curPos = newPos;
            }
            moves.Reverse();
            return TwitchMove(moves.Join(""));
        }

        private IEnumerable<object> TwitchMove(string moves)
        {
            for (var i = 0; i < moves.Length; i++)
            {
                _buttons["urdl".IndexOf(moves[i])].OnInteract();
                yield return new WaitForSeconds(.2f);
            }
        }
    }
}