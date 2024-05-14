using UIKit;
using CoreGraphics;
using Foundation;

namespace MonoTouch.Dialog
{
	public class ActivityElement : Element {
		public ActivityElement () : base ("")
		{
		}

		UIActivityIndicatorView? _indicator;

		public bool Animating {
			get => _indicator is { IsAnimating: true };
			set {
				if (value)
					_indicator?.StartAnimating ();
				else
					_indicator?.StopAnimating ();
			}
		}

		static readonly NSString Ikey = new NSString ("ActivityElement");

		protected override NSString CellKey => Ikey;

		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = tv.DequeueReusableCell (CellKey) ?? new UITableViewCell (UITableViewCellStyle.Default, CellKey);

			_indicator = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.Gray);
			var sBounds = tv.Frame;
			var vBounds = _indicator.Bounds;

			_indicator.Frame = new CGRect((sBounds.Width-vBounds.Width)/2, 12, vBounds.Width, vBounds.Height);
			_indicator.StartAnimating ();

			cell.Add (_indicator);

			return cell;
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing){
				if (_indicator != null){
					_indicator.Dispose ();
					_indicator = null;
				}
			}
			base.Dispose (disposing);
		}
	}
}

