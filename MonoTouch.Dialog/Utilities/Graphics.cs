using CoreGraphics;

namespace MonoTouch.Dialog
{
	public static class GraphicsUtil {
		
		/// <summary>
		///    Creates a path for a rectangle with rounded corners
		/// </summary>
		/// <param name="rect">
		/// The <see cref="CGRect"/> rectangle bounds
		/// </param>
		/// <param name="radius">
		/// The <see cref="System.Single"/> size of the rounded corners
		/// </param>
		/// <returns>
		/// A <see cref="CGPath"/> that can be used to stroke the rounded rectangle
		/// </returns>
		public static CGPath MakeRoundedRectPath (CGRect rect, nfloat radius)
		{
			var minX = rect.Left;
			var midX = rect.Left + (rect.Width)/2;
			var maxX = rect.Right;
			var minY = rect.Top;
			var midY = rect.Y+rect.Size.Height/2;
			var maxY = rect.Bottom;

			var path = new CGPath ();
			path.MoveToPoint (minX, midY);
			path.AddArcToPoint (minX, minY, midX, minY, radius);
			path.AddArcToPoint (maxX, minY, maxX, midY, radius);
			path.AddArcToPoint (maxX, maxY, midX, maxY, radius);
			path.AddArcToPoint (minX, maxY, minX, midY, radius);		
			path.CloseSubpath ();
			
			return path;
        }
		
		public static void FillRoundedRect (CGContext ctx, CGRect rect, nfloat radius)
		{
				var p = GraphicsUtil.MakeRoundedRectPath (rect, radius);
				ctx.AddPath (p);
				ctx.FillPath ();
		}

		public static CGPath MakeRoundedPath (float size, float radius)
		{
			var hSize = size/2;
			
			var path = new CGPath ();
			path.MoveToPoint (size, hSize);
			path.AddArcToPoint (size, size, hSize, size, radius);
			path.AddArcToPoint (0, size, 0, hSize, radius);
			path.AddArcToPoint (0, 0, hSize, 0, radius);
			path.AddArcToPoint (size, 0, size, hSize, radius);
			path.CloseSubpath ();
			
			return path;
		}
	}
}

