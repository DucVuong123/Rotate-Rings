using System;
using System.Collections.Generic;
using RotateRings.Gameplay.Shapes;
using UnityEngine;

namespace RotateRings.Gameplay.Data
{
    /// <summary>A level as stored in JSON (JsonUtility-friendly: flat, no polymorphism).</summary>
    [Serializable]
    public class LevelDefinition
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public string name = "New Level";
        public float tubeRadius = 0.08f;
        public float clipHalfSize = 0.1f;
        public List<PieceDef> pieces = new();
        public List<LinkDef> links = new();

        public PieceDef FindPiece(string id) => pieces.Find(p => p.id == id);
        public LinkDef FindLink(string id) => links.Find(l => l.id == id);
    }

    [Serializable]
    public class PieceDef
    {
        public string id;

        /// <summary>Shape template id ("arc", "bar", "elbow"...).</summary>
        public string shape;

        /// <summary>"#RRGGBB".</summary>
        public string color = "#FFFFFF";

        public Vector2 position;

        /// <summary>Degrees CCW. For arcs this is the direction the gap faces.</summary>
        public float rotation;

        /// <summary>"rotate", "slide" or "fixed".</summary>
        public string motion = "rotate";

        public ShapeParams parameters;

        public MotionType Motion
        {
            get => MotionNames.Parse(motion);
            set => motion = MotionNames.ToName(value);
        }

        public Color Color
        {
            get => ColorUtility.TryParseHtmlString(color, out var c) ? c : Color.white;
            set => color = "#" + ColorUtility.ToHtmlStringRGB(value);
        }
    }

    [Serializable]
    public class LinkDef
    {
        public const string ClipKind = "clip";
        public const string PinKind = "pin";

        public string id;

        /// <summary>"clip" (owned by a piece) or "pin" (fixed to the board).</summary>
        public string kind = ClipKind;

        /// <summary>Owning piece for clips; empty for pins.</summary>
        public string ownerId;

        public string targetId;

        /// <summary>World point where the clip head grips the target's tube.</summary>
        public Vector2 point;

        public bool IsPin => kind == PinKind;
    }

    public static class MotionNames
    {
        public static MotionType Parse(string name) => name switch
        {
            "slide" => MotionType.Slide,
            "fixed" => MotionType.Fixed,
            _ => MotionType.Rotate,
        };

        public static string ToName(MotionType motion) => motion switch
        {
            MotionType.Slide => "slide",
            MotionType.Fixed => "fixed",
            _ => "rotate",
        };
    }

    public static class LevelJson
    {
        public static LevelDefinition Parse(string json)
        {
            var level = JsonUtility.FromJson<LevelDefinition>(json);
            if (level == null)
                throw new FormatException("Level JSON is empty or invalid.");

            level.pieces ??= new List<PieceDef>();
            level.links ??= new List<LinkDef>();
            return level;
        }

        public static string ToJson(LevelDefinition level) => JsonUtility.ToJson(level, true);

        public static LevelDefinition Clone(LevelDefinition level) => Parse(ToJson(level));
    }
}
