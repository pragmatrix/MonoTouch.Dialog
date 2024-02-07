//
// This cell does not perform cell recycling, do not use as
// sample code for new elements. 
//
using System;
using System.Diagnostics;
using Foundation;
using UIKit;
using CoreGraphics;
using JetBrains.Annotations;

namespace MonoTouch.Dialog
{
	[PublicAPI]
	public class LoadMoreElement : Element, IElementSizing
	{
		static readonly NSString Key = new("LoadMoreElement");
		public string? NormalCaption { get; set; }
		public string? LoadingCaption { get; set; }
		public UIColor? TextColor { get; set; }
		public UIColor? BackgroundColor { get; set; }
		public event Action<LoadMoreElement>? Tapped;
		public UIFont Font;
		public float? Height;
		bool _animating;
		
		public LoadMoreElement () : base ("")
		{
			Font = UIFont.BoldSystemFontOfSize(16);
		}
		
		public LoadMoreElement (string? normalCaption, string? loadingCaption, Action<LoadMoreElement>? tapped) 
			: this (normalCaption, loadingCaption, tapped, UIFont.BoldSystemFontOfSize (16), UIColor.Black)
		{
		}
		
		public LoadMoreElement (string? normalCaption, string? loadingCaption, Action<LoadMoreElement>? tapped, UIFont? font, UIColor? textColor) 
			: base ("")
		{
			NormalCaption = normalCaption;
			LoadingCaption = loadingCaption;
			Tapped += tapped;
			Font = font ?? UIFont.BoldSystemFontOfSize(16);
			TextColor = textColor;
		}
		
		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = tv.DequeueReusableCell (Key);
			UIActivityIndicatorView? activityIndicator;
			UILabel? caption;
			
			if (cell == null){
				cell = new UITableViewCell (UITableViewCellStyle.Default, Key);
			
				activityIndicator = new UIActivityIndicatorView () {
					ActivityIndicatorViewStyle = UIActivityIndicatorViewStyle.Gray,
					Tag = 1
				};
				caption = new UILabel () {
					AdjustsFontSizeToFitWidth = false,
					AutoresizingMask = UIViewAutoresizing.FlexibleWidth,
					Tag = 2
				};
				cell.ContentView.AddSubview (caption);
				cell.ContentView.AddSubview (activityIndicator);
			} else
			{
				activityIndicator = cell.ContentView.ViewWithTag(1) as UIActivityIndicatorView;
				caption =  cell.ContentView.ViewWithTag (2) as UILabel;
			}
			
			Trace.Assert(activityIndicator is not null && caption is not null);
			
			if (Animating) {
				caption.Text = LoadingCaption;
				activityIndicator.Hidden = false;
				activityIndicator.StartAnimating ();
			} else {
				caption.Text = NormalCaption;
				activityIndicator.Hidden = true;
				activityIndicator.StopAnimating ();
			}
			if (BackgroundColor != null){
				cell.ContentView.BackgroundColor = BackgroundColor ?? UIColor.Clear;
			} else {
				cell.ContentView.BackgroundColor = null;
			}
			caption.BackgroundColor = UIColor.Clear;
			caption.TextColor = TextColor ?? UIColor.Black;
			caption.Font = Font;
			caption.TextAlignment = Alignment;
			Layout (cell, activityIndicator, caption);
			return cell;
		}
		
		public bool Animating {
			get => _animating;
			set {
				if (_animating == value)
					return;
				_animating = value;
				var cell = GetActiveCell ();
				if (cell == null)
					return;
				var activityIndicator = cell.ContentView.ViewWithTag (1) as UIActivityIndicatorView;
				var caption = cell.ContentView.ViewWithTag (2) as UILabel;

				Trace.Assert(activityIndicator is not null && caption is not null);
				
				if (value) {
					caption.Text = LoadingCaption;
					activityIndicator.Hidden = false;
					activityIndicator.StartAnimating ();
				} else {
					activityIndicator.StopAnimating ();
					activityIndicator.Hidden = true;
					caption.Text = NormalCaption;
				}
				Layout (cell, activityIndicator, caption);
			}
		}
				
		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
		{
			tableView.DeselectRow (path, true);
			
			if (Animating)
				return;
			
			if (Tapped != null){
				Animating = true;
				Tapped (this);
			}
		}
		
		CGSize GetTextSize (string? text)
		{
			return new NSString (text).StringSize (Font, (float)UIScreen.MainScreen.Bounds.Width, UILineBreakMode.TailTruncation);
		}
		
		const int Pad = 10;
		const int Size = 20;
		
		public nfloat GetHeight (UITableView tableView, NSIndexPath indexPath)
		{
			return Height ?? GetTextSize (Animating ? LoadingCaption : NormalCaption).Height + 2*Pad;
		}
		
		void Layout (UITableViewCell cell, UIActivityIndicatorView activityIndicator, UILabel caption)
		{
			var sBounds = cell.ContentView.Bounds;

			var size = GetTextSize (Animating ? LoadingCaption : NormalCaption);
			
			if (!activityIndicator.Hidden)
				activityIndicator.Frame = new CGRect ((sBounds.Width-size.Width)/2-Size*2, Pad, Size, Size);

			caption.Frame = new CGRect (10, Pad, sBounds.Width-20, size.Height);
		}
		
		public UITextAlignment Alignment { get; set; } = UITextAlignment.Center;

		public UITableViewCellAccessory Accessory { get; set; }
	}
}

