//
// ElementBadge.cs: defines the Badge Element.
//
// Author:
//   Miguel de Icaza (miguel@gnome.org)
//
// Copyright 2010, Novell, Inc.
//
// Code licensed under the MIT X11 license
//
using System;

using UIKit;
using CoreGraphics;
using Foundation;
using JetBrains.Annotations;
using NSAction = System.Action;

namespace MonoTouch.Dialog
{	
	/// <summary>
	///    This element can be used to show an image with some text
	/// </summary>
	/// <remarks>
	///    The font can be configured after the element has been created
	///    by assigning to the Font property;   If you want to render
	///    multiple lines of text, set the MultiLine property to true.
	/// 
	///    If no font is specified, it will default to Helvetica 17.
	/// 
	///    A static method MakeCalendarBadge is provided that can 
	///    render a calendar badge like the iPhone OS.   It will compose
	///    the text on top of the image which is expected to be 57x57
	/// </remarks>
	[PublicAPI]
	public class BadgeElement : Element, IElementSizing {
		static readonly NSString CKey = new("badgeKey");
		public event NSAction? Tapped;
		public UILineBreakMode LineBreakMode = UILineBreakMode.TailTruncation;
		public UIViewContentMode ContentMode = UIViewContentMode.Left;
		public int Lines = 1;
		public UITableViewCellAccessory Accessory = UITableViewCellAccessory.None;
		readonly UIImage _image;
		UIFont? _font;

		public BadgeElement (UIImage badgeImage, string cellText, NSAction? tapped = null) : base (cellText)
		{
			_image = badgeImage ?? throw new ArgumentNullException (nameof(badgeImage));
			if (tapped != null)
				Tapped += tapped;
		}		
	
		public UIFont Font {
			get {
				if (_font == null)
					_font = UIFont.FromName ("Helvetica", 17f);
				return _font;
			}
			set {
				if (_font != null)
					_font.Dispose ();
				_font = value;
			}
		}
		
		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = tv.DequeueReusableCell (CKey) ?? new UITableViewCell (UITableViewCellStyle.Default, CKey) {
				SelectionStyle = UITableViewCellSelectionStyle.Blue
			};
			cell.Accessory = Accessory;
			var tl = cell.TextLabel;
			tl.Text = Caption;
			tl.Font = Font;
			tl.LineBreakMode = LineBreakMode;
			tl.Lines = Lines;
			tl.ContentMode = ContentMode;
			
			cell.ImageView.Image = _image;
			
			return cell;
		}

		public nfloat GetHeight (UITableView tableView, NSIndexPath indexPath)
		{
			CGSize size = new CGSize (tableView.Bounds.Width - 40, nfloat.MaxValue);
			nfloat height = Caption.StringSize (Font, size, LineBreakMode).Height + 10;
			
			// Image is 57 pixels tall, add some padding
			return (nfloat)Math.Max (height, 63);
		}

		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
		{
			if (Tapped != null)
				Tapped ();
			tableView.DeselectRow (path, true);
		}
		
		public static UIImage? MakeCalendarBadge (UIImage template, string smallText, string bigText)
		{
			using var cs = CGColorSpace.CreateDeviceRGB ();
			using var context = new CGBitmapContext (IntPtr.Zero, 57, 57, 8, 57*4, cs, CGImageAlphaInfo.PremultipliedLast);
			//context.ScaleCTM (0.5f, -1);
			context.TranslateCTM (0, 0);
			context.DrawImage (new CGRect (0, 0, 57, 57), template.CGImage);
			context.SetFillColor (1, 1, 1, 1);
					
			context.SelectFont ("Helvetica", 10f, CGTextEncoding.MacRoman);
					
			// Pretty lame way of measuring strings, as documented:
			var start = context.TextPosition.X;					
			context.SetTextDrawingMode (CGTextDrawingMode.Invisible);
			context.ShowText (smallText);
			var width = context.TextPosition.X - start;
					
			context.SetTextDrawingMode (CGTextDrawingMode.Fill);
			context.ShowTextAtPoint ((57-width)/2, 46, smallText);
					
			// The big string
			context.SelectFont ("Helvetica-Bold", 32, CGTextEncoding.MacRoman);					
			start = context.TextPosition.X;
			context.SetTextDrawingMode (CGTextDrawingMode.Invisible);
			context.ShowText (bigText);
			width = context.TextPosition.X - start;
					
			context.SetFillColor (0, 0, 0, 1);
			context.SetTextDrawingMode (CGTextDrawingMode.Fill);
			context.ShowTextAtPoint ((57-width)/2, 9, bigText);
					
			context.StrokePath ();

			var image = context.ToImage();
			return image is not null ? UIImage.FromImage (image) : null;
		}
	}
}
