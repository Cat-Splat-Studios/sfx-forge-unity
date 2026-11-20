using System.Collections.Generic;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// A shape with the game's Ink outline: "anything you can touch (other than the ground) has an Ink outline", made
    /// as a slightly larger Ink copy behind the shape. Add the convex pieces the shape is made of; when it is written
    /// out, the Ink copies of ALL pieces go first and the coloured pieces on top, so pieces that overlap merge into one
    /// silhouette with one outline round it.
    /// </summary>
    public sealed class InkedShape
    {
        struct Piece
        {
            public IReadOnlyList<Vector2> Points;
            public Color Color;
            public bool Outlined;
        }

        readonly List<Piece> pieces = new List<Piece>();

        /// <summary>Adds a convex piece of the silhouette: filled with the colour, outlined in Ink.</summary>
        public InkedShape Add(IReadOnlyList<Vector2> convexPolygon, Color fill)
        {
            pieces.Add(new Piece { Points = convexPolygon, Color = fill, Outlined = true });
            return this;
        }

        /// <summary>Adds a convex detail drawn on top without an outline of its own (eyes, a stripe).</summary>
        public InkedShape AddDetail(IReadOnlyList<Vector2> convexPolygon, Color color)
        {
            pieces.Add(new Piece { Points = convexPolygon, Color = color, Outlined = false });
            return this;
        }

        /// <summary>Writes the outline and then the pieces, in the order they were added.</summary>
        public void AddTo(MeshBuilder builder)
        {
            foreach (Piece piece in pieces)
                if (piece.Outlined) builder.AddPolygon(Shapes.Outset(piece.Points, ArtStyle.OutlineWidth), Palette.Ink);
            foreach (Piece piece in pieces)
                builder.AddPolygon(piece.Points, piece.Color);
        }

        /// <summary>The shape as a new mesh.</summary>
        public Mesh ToMesh(string name)
        {
            var builder = new MeshBuilder();
            AddTo(builder);
            return builder.ToMesh(name);
        }
    }
}
