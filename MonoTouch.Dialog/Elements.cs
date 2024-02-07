//
// Elements.cs: defines the various components of our view
//
// Author:
//   Miguel de Icaza (miguel@gnome.org)
//
// Copyright 2010, Novell, Inc.
//
// Code licensed under the MIT X11 license
//
// TODO: StyledStringElement: merge with multi-line?
// TODO: StyledStringElement: add image scaling features?
// TODO: StyledStringElement: add sizing based on image size?
// TODO: Move image rendering to StyledImageElement, reason to do this: the image loader would only be imported in this case, linked out otherwise
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using UIKit;
using CoreGraphics;
using Foundation;
using JetBrains.Annotations;
using MonoTouch.Dialog.Utilities;

using NSAction = System.Action;

namespace MonoTouch.Dialog
{
	/// <summary>
	/// Base class for all elements in MonoTouch.Dialog
	/// </summary>
	[PublicAPI]
	public class Element : IDisposable {
		/// <summary>
		///  Handle to the container object.
		/// </summary>
		/// <remarks>
		/// For sections this points to a RootElement, for every
		/// other object this points to a Section and it is null
		/// for the root RootElement.
		/// </remarks>
		public Element? Parent;
		
		/// <summary>
		///  The caption to display for this given element
		/// </summary>
		public string? Caption;
		
		/// <summary>
		///  Initializes the element with the given caption.
		/// </summary>
		/// <param name="caption">
		/// The caption.
		/// </param>
		public Element (string? caption)
		{
			this.Caption = caption;
		}	
		
		public void Dispose ()
		{
			Dispose (true);
		}

		protected virtual void Dispose (bool disposing)
		{
		}

		/// <summary>
		/// Subclasses that override the GetCell method should override this method as well
		/// </summary>
		/// <remarks>
		/// This method should return the key passed to UITableView.DequeueReusableCell.
		/// If your code overrides the GetCell method to change the cell, you must also 
		/// override this method and return a unique key for it.
		/// 
		/// This works in most subclasses with a couple of exceptions: StringElement and
		/// various derived classes do not use this setting as they need a wider range
		/// of keys for different uses, so you need to look at the source code for those
		/// if you are trying to override StringElement or StyledStringElement.
		/// </remarks>
		protected virtual NSString CellKey => new("xx");

		/// <summary>
		/// Gets a UITableViewCell for this element.   Can be overridden, but if you 
		/// customize the style or contents of the cell you must also override the CellKey 
		/// property in your derived class.
		/// </summary>
		public virtual UITableViewCell GetCell (UITableView tv)
		{
			return new UITableViewCell (UITableViewCellStyle.Default, CellKey);
		}
		
		protected static void RemoveTag (UITableViewCell cell, int tag)
		{
			var viewToRemove = cell.ContentView.ViewWithTag (tag);
			// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
			if (viewToRemove != null)
				viewToRemove.RemoveFromSuperview ();
		}
		
		/// <summary>
		/// Returns a summary of the value represented by this object, suitable 
		/// for rendering as the result of a RootElement with child objects.
		/// </summary>
		/// <returns>
		/// The return value must be a short description of the value.
		/// </returns>
		public virtual string Summary ()
		{
			return "";
		}
		
		/// <summary>
		/// Invoked when the given element has been deselected by the user.
		/// </summary>
		/// <param name="dvc">
		/// The <see cref="DialogViewController"/> where the deselection took place
		/// </param>
		/// <param name="tableView">
		/// The <see cref="UITableView"/> that contains the element.
		/// </param>
		/// <param name="path">
		/// The <see cref="NSIndexPath"/> that contains the Section and Row for the element.
		/// </param>
		public virtual void Deselected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
		{
		}
		
		/// <summary>
		/// Invoked when the given element has been selected by the user.
		/// </summary>
		/// <param name="dvc">
		/// The <see cref="DialogViewController"/> where the selection took place
		/// </param>
		/// <param name="tableView">
		/// The <see cref="UITableView"/> that contains the element.
		/// </param>
		/// <param name="path">
		/// The <see cref="NSIndexPath"/> that contains the Section and Row for the element.
		/// </param>
		public virtual void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
		{
		}

		/// <summary>
		/// If the cell is attached will return the immediate RootElement
		/// </summary>
		public RootElement? GetImmediateRootElement ()
		{
				var section = Parent as Section ?? this as Section;
				return section?.Parent as RootElement;
		}
		
		/// <summary>
		/// Returns the UITableView associated with this element, or null if this cell is not currently attached to a UITableView
		/// </summary>
		public UITableView? GetContainerTableView ()
		{
			var root = GetImmediateRootElement ();
			if (root == null)
				return null;
			return root.TableView;
		}
		
		/// <summary>
		/// Returns the currently active UITableViewCell for this element, or null if the element is not currently visible
		/// </summary>
		public UITableViewCell? GetActiveCell ()
		{
			var tv = GetContainerTableView ();
			if (tv == null)
				return null;
			var path = IndexPath;
			if (path == null)
				return null;
			return tv.CellAt (path);
		}
		
		/// <summary>
		///  Returns the IndexPath of a given element.   This is only valid for leaf elements,
		///  it does not work for a toplevel RootElement or a Section of if the Element has
		///  not been attached yet.
		/// </summary>
		public NSIndexPath? IndexPath { 
			get {
				var section = Parent as Section;
				if (section == null)
					return null;
				var root = section.Parent as RootElement;
				if (root == null)
					return null;
				
				int row = 0;
				foreach (var element in section.Elements){
					if (element == this){
						int nSect = 0;
						foreach (var sect in root.Sections){
							if (section == sect){
								return NSIndexPath.FromRowSection (row, nSect);
							}
							nSect++;
						}
					}
					row++;
				}
				return null;
			}
		}
		
		/// <summary>
		///   Method invoked to determine if the cell matches the given text, never invoked with a null value or an empty string.
		/// </summary>
		public virtual bool Matches (string text)
		{
			if (Caption == null)
				return false;
			return Caption.IndexOf (text, StringComparison.CurrentCultureIgnoreCase) != -1;
		}
	}

	[PublicAPI]
	public abstract class BoolElement : Element {
		bool _val;
		public virtual bool Value {
			get => _val;
			set {
				var emit = _val != value;
				_val = value;
				if (emit && ValueChanged != null)
					ValueChanged (this, EventArgs.Empty);
			}
		}
		public event EventHandler? ValueChanged;

		protected BoolElement (string caption, bool value) : base (caption)
		{
			_val = value;
		}
		
		public override string Summary ()
		{
			return _val ? "On".GetText () : "Off".GetText ();
		}		
	}
	
	/// <summary>
	/// Used to display switch on the screen.
	/// </summary>
	public class BooleanElement : BoolElement {
		static readonly NSString BKey = new("BooleanElement");
#if !__TVOS__
		UISwitch? _sw;
#endif // !__TVOS__
		
		public BooleanElement (string caption, bool value) : base (caption, value)
		{  }

		public BooleanElement(string caption, bool value, string key) : base(caption, value)
		{
		}
		
		protected override NSString CellKey => BKey;

		public override UITableViewCell GetCell (UITableView tv)
		{
#if __TVOS__
			var cell = ConfigCell (base.GetCell (tv));
#else
			if (_sw == null){
				_sw = new UISwitch
				{
					BackgroundColor = UIColor.Clear,
					Tag = 1,
					On = Value
				};
				_sw.AddTarget (delegate {
					Value = _sw.On;
				}, UIControlEvent.ValueChanged);
			} else
				_sw.On = Value;
			
			var cell = tv.DequeueReusableCell (CellKey);
			if (cell == null){
				cell = new UITableViewCell (UITableViewCellStyle.Default, CellKey);
				cell.SelectionStyle = UITableViewCellSelectionStyle.None;
			} else
				RemoveTag (cell, 1);
		
			cell.TextLabel.Text = Caption;
			cell.AccessoryView = _sw;
#endif // !__TVOS__
			return cell;
		}

#if __TVOS__
		UITableViewCell ConfigCell (UITableViewCell cell)
		{
			cell.Accessory = Value ? UITableViewCellAccessory.Checkmark : UITableViewCellAccessory.None;
			cell.TextLabel.Text = Caption;
			return cell;
		}

		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
		{
			Value = !Value;
			var cell = tableView.CellAt (path);
			ConfigCell (cell);
			base.Selected (dvc, tableView, path);
		}
#endif // !__TVOS__

		protected override void Dispose (bool disposing)
		{
			if (disposing){
#if !__TVOS__
				if (_sw != null){
					_sw.Dispose ();
					_sw = null;
				}
#endif // !__TVOS__
			}
		}
		
		public override bool Value {
			get {
				return base.Value;
			}
			set {
				 base.Value = value;
#if __TVOS__
				// Not sure what to do here
#else
				if (_sw != null)
					_sw.On = value;
#endif  // !__TVOS__
			}
		}
	}
	
	/// <summary>
	///  This class is used to render a string + a state in the form
	/// of an image.  
	/// </summary>
	/// <remarks>
	/// It is abstract to avoid making this element
	/// keep two pointers for the state images, saving 8 bytes per
	/// slot.   The more derived class "BooleanImageElement" shows
	/// one way to implement this by keeping two pointers, a better
	/// implementation would return pointers to images that were 
	/// preloaded and are static.
	/// 
	/// A subclass only needs to implement the GetImage method.
	/// </remarks>
	[PublicAPI]
	public abstract class BaseBooleanImageElement : BoolElement {
		static readonly NSString Key = new("BooleanImageElement");

		public class TextWithImageCellView : UITableViewCell {
			const int FontSize = 17;
			static readonly UIFont Font = UIFont.BoldSystemFontOfSize (FontSize);
			BaseBooleanImageElement _parent;
			readonly UILabel _label;
			readonly UIButton _button;
			const int ImageSpace = 32;
			const int Padding = 8;
	
			public TextWithImageCellView (BaseBooleanImageElement parent) : base (UITableViewCellStyle.Value1, parent.CellKey)
			{
				_parent = parent;
				_label = new UILabel () {
					TextAlignment = UITextAlignment.Left,
					Text = _parent.Caption,
					Font = Font,
					BackgroundColor = UIColor.Clear
				};
				_button = UIButton.FromType (UIButtonType.Custom);
				_button.TouchDown += delegate {
					_parent.Value = !_parent.Value;
					UpdateImage ();
					if (_parent.Tapped != null)
						_parent.Tapped ();
				};
				base.ContentView.Add (_label);
				base.ContentView.Add (_button);
				UpdateImage ();
			}

			void UpdateImage ()
			{
				_button.SetImage (_parent.GetImage (), UIControlState.Normal);
			}
			
			public override void LayoutSubviews ()
			{
				base.LayoutSubviews ();
				var full = ContentView.Bounds;
				var frame = full;
				frame.Height = 22;
				frame.X = Padding;
				frame.Y = (full.Height-frame.Height)/2;
				frame.Width -= ImageSpace+Padding;
				_label.Frame = frame;
				
				_button.Frame = new CGRect (full.Width-ImageSpace, -3, ImageSpace, 48);
			}
			
			public void UpdateFrom (BaseBooleanImageElement newParent)
			{
				_parent = newParent;
				UpdateImage ();
				_label.Text = _parent.Caption;
				SetNeedsDisplay ();
			}
		}

		protected BaseBooleanImageElement (string caption, bool value)
			: base (caption, value)
		{
		}
		
		public event NSAction? Tapped;
		
		protected abstract UIImage? GetImage ();
		
		protected override NSString CellKey => Key;

		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = tv.DequeueReusableCell (CellKey) as TextWithImageCellView;
			if (cell == null)
				cell = new TextWithImageCellView (this);
			else
				cell.UpdateFrom (this);
			return cell;
		}
	}
	
	public class BooleanImageElement : BaseBooleanImageElement
	{
		UIImage? _onImage;
		public UIImage? OffImage;
		
		public BooleanImageElement (string caption, bool value, UIImage? onImage, UIImage? offImage) : base (caption, value)
		{
			_onImage = onImage;
			OffImage = offImage;
		}
		
		protected override UIImage? GetImage ()
		{
			return Value ? _onImage : OffImage;
		}

		protected override void Dispose (bool disposing)
		{
			base.Dispose (disposing);
			_onImage = null;
			OffImage = null;
		}
	}
	
	/// <summary>
	///  Used to display a slider on the screen.
	/// </summary>
	public class FloatElement : Element {
		public bool ShowCaption;
		public float Value;
		public float MinValue, MaxValue;
		static readonly NSString SKey = new("FloatElement");
		//UIImage Left, Right;
#if !__TVOS__
		// There is no UISlider in tvOS, so make this read-only for now.
		UISlider? _slider;
#endif // !__TVOS__

		public FloatElement (float value) : this (null, null, value)
		{
		}
		
		public FloatElement (UIImage? left, UIImage? right, float value) : base (null)
		{
			_ = left;
			_ = right;
			//Left = left;
			//Right = right;
			MinValue = 0;
			MaxValue = 1;
			Value = value;
		}
		
		protected override NSString CellKey => SKey;

		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = tv.DequeueReusableCell (CellKey);
			if (cell == null){
				cell = new UITableViewCell (UITableViewCellStyle.Default, CellKey);
				cell.SelectionStyle = UITableViewCellSelectionStyle.None;
			} else
				RemoveTag (cell, 1);

#if __TVOS__
			if (Caption != null && ShowCaption)
				cell.TextLabel.Text = Caption;

			cell.DetailTextLabel.Text = string.Format ("[{0}...{2}]: {1}]", MinValue, Value, MaxValue);
#else
			CGSize captionSize = new CGSize (0, 0);
			if (Caption != null && ShowCaption){
				cell.TextLabel.Text = Caption;
				captionSize = Caption.StringSize (UIFont.FromName (cell.TextLabel.Font.Name, UIFont.LabelFontSize));
				captionSize.Width += 10; // Spacing
			}
			if (_slider == null){
				_slider = new UISlider (new CGRect (10f + captionSize.Width, UIDevice.CurrentDevice.CheckSystemVersion (7, 0) ? 18f : 12f, cell.ContentView.Bounds.Width - 20 - captionSize.Width, 7f)) {
					BackgroundColor = UIColor.Clear,
					MinValue = this.MinValue,
					MaxValue = this.MaxValue,
					Continuous = true,
					Value = this.Value,
					Tag = 1,
					AutoresizingMask = UIViewAutoresizing.FlexibleWidth
				};
				_slider.ValueChanged += delegate {
					Value = _slider.Value;
				};
			} else {
				_slider.Value = Value;
			}
			
			cell.ContentView.AddSubview (_slider);
#endif // __TVOS__
			return cell;
		}

		public override string Summary ()
		{
			return Value.ToString (CultureInfo.CurrentCulture);
		}
		
		protected override void Dispose (bool disposing)
		{
			if (disposing){
#if !__TVOS__
				if (_slider != null){
					_slider.Dispose ();
					_slider = null;
				}
#endif // !__TVOS__
			}
		}		
	}

	/// <summary>
	///  Used to display a cell that will launch a web browser when selected.
	/// </summary>
	public class HtmlElement : Element {
		NSUrl _nsUrl;
		static readonly NSString HKey = new("HtmlElement");
#if !__TVOS__ && !__MACCATALYST__
		// There is no UIWebView in tvOS, so we can't launch anything.
		UIWebView? _web;
#endif // !__TVOS__ && !__MACCATALYST__
		
		public HtmlElement (string caption, string url) : this(caption, new NSUrl(url))
		{
		}
		
		public HtmlElement (string caption, NSUrl url) : base (caption)
		{
			_nsUrl = url;
		}
		
		protected override NSString CellKey => HKey;

		public string Url {
			get => _nsUrl.ToString ();
			set => _nsUrl = new NSUrl (value);
		}
		
		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = tv.DequeueReusableCell (CellKey);
			if (cell == null){
				cell = new UITableViewCell (UITableViewCellStyle.Default, CellKey);
				cell.SelectionStyle = UITableViewCellSelectionStyle.Blue;
			}
			cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
			
			cell.TextLabel.Text = Caption;
			return cell;
		}

		static bool NetworkActivity {
			set {
#if !__TVOS__
				UIApplication.SharedApplication.NetworkActivityIndicatorVisible = value;
#endif // !__TVOS__
			}
		}
		
#if !__TVOS__ && !__MACCATALYST__
		// We use this class to dispose the web control when it is not
		// in use, as it could be a bit of a pig, and we do not want to
		// wait for the GC to kick-in.
		class WebViewController : UIViewController {
#pragma warning disable 414
			HtmlElement _container;
#pragma warning restore 414
			
			public WebViewController (HtmlElement container)
			{
				_container = container;
			}
			
			public bool Autorotate { get; init; }

			public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
			{
				return Autorotate;
			}
		}
		
		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
		{
			int i = 0;
			var vc = new WebViewController (this) {
				Autorotate = dvc.Autorotate
			};

			_web = new UIWebView (UIScreen.MainScreen.Bounds) {
				BackgroundColor = UIColor.White,
				ScalesPageToFit = true,
				AutoresizingMask = UIViewAutoresizing.All
			};
			_web.LoadStarted += delegate {
				// this is called several times and only one UIActivityIndicatorView is needed
				if (i++ == 0) {
					var indicator = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.White);
					vc.NavigationItem.RightBarButtonItem = new UIBarButtonItem (indicator);
					indicator.StartAnimating ();
				}
				NetworkActivity = true;
			};
			_web.LoadFinished += delegate {
				if (--i == 0) {
					// we stopped loading, remove indicator and dispose of UIWebView
					vc.NavigationItem.RightBarButtonItem = null;
					_web.StopLoading ();
					_web.Dispose ();
				}
				NetworkActivity = false;
			};
			_web.LoadError += (webview, args) => {
				NetworkActivity = false;
				vc.NavigationItem.RightBarButtonItem = null;
				_web.LoadHtmlString (
					String.Format ("<html><center><font size=+5 color='red'>{0}:<br>{1}</font></center></html>",
					"An error occurred:".GetText (), args.Error.LocalizedDescription), null);
			};
			vc.NavigationItem.Title = Caption;
			
			Trace.Assert(vc.View is not null);
			vc.View.AutosizesSubviews = true;
			vc.View.AddSubview (_web);
			
			dvc.ActivateController (vc);
			_web.LoadRequest (NSUrlRequest.FromUrl (_nsUrl));
		}
#endif // !__TVOS__ && !__MACCATALYST__
	}

	/// <summary>
	///   The string element can be used to render some text in a cell 
	///   that can optionally respond to tap events.
	/// </summary>
	[PublicAPI]
	public class StringElement : Element {
		static readonly NSString SKey = new("StringElement");
		static readonly NSString SKeyValue = new("StringElementValue");
		public UITextAlignment Alignment = UITextAlignment.Left;
		public string? Value;
		
		public StringElement (string caption) : base (caption) {}
		
		public StringElement (string caption, string? value) : base (caption)
		{
			Value = value;
		}
		
		public StringElement (string caption,  NSAction tapped) : base (caption)
		{
			Tapped += tapped;
		}
		
		public event NSAction? Tapped;
				
		public override UITableViewCell GetCell (UITableView tv)
		{
			// var cell = tv.DequeueReusableCell (Value == null ? SKey : SKeyValue);
			UITableViewCell? cell = null;
			if (cell == null){
				cell = new UITableViewCell (Value == null ? UITableViewCellStyle.Default : UITableViewCellStyle.Value1, Value == null ? SKey : SKeyValue);
				cell.SelectionStyle = Tapped != null ? UITableViewCellSelectionStyle.Blue : UITableViewCellSelectionStyle.None;
			}
			cell.Accessory = UITableViewCellAccessory.None;
			cell.TextLabel.Text = Caption;
			cell.TextLabel.TextAlignment = Alignment;
			
			// The check is needed because the cell might have been recycled.
			if (cell.DetailTextLabel != null)
				cell.DetailTextLabel.Text = Value ?? "";
			
			return cell;
		}

		public override string Summary ()
		{
			return Caption ?? "";
		}
		
		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath indexPath)
		{
			if (Tapped != null)
				Tapped ();
			tableView.DeselectRow (indexPath, true);
		}
		
		public override bool Matches (string text)
		{
			return (Value != null && Value.IndexOf (text, StringComparison.CurrentCultureIgnoreCase) != -1) || base.Matches (text);
		}
	}
	
	/// <summary>
	///   A version of the StringElement that can be styled with a number of formatting 
	///   options and can render images or background images either from UIImage parameters 
	///   or by downloading them from the net.
	/// </summary>
	[PublicAPI]
	public class StyledStringElement : StringElement, IImageUpdated, IColorizeBackground {
		static readonly NSString [] SKey = [new(".1"), new(".2"), new(".3"), new(".4")];
		
		public StyledStringElement (string caption) : base (caption) {}
		public StyledStringElement (string caption, NSAction tapped) : base (caption, tapped) {}
		public StyledStringElement (string caption, string value) : base (caption, value) 
		{
			Style = UITableViewCellStyle.Value1;	
		}
		public StyledStringElement (string caption, string value, UITableViewCellStyle style) : base (caption, value) 
		{ 
			this.Style = style;
		}
		
		protected UITableViewCellStyle Style;
		public event NSAction? AccessoryTapped;
		public UIFont? Font;
		public UIFont? SubtitleFont;
		public UIColor? TextColor;
		public UILineBreakMode LineBreakMode = UILineBreakMode.WordWrap;
		public int Lines = 0;
		public UITableViewCellAccessory Accessory = UITableViewCellAccessory.None;
		
		// To keep the size down for a StyleStringElement, we put all the image information
		// on a separate structure, and create this on demand.
		ExtraInfo? _extraInfo;
		
		class ExtraInfo {
			public UIImage? Image; // Maybe add BackgroundImage?
			public UIColor? BackgroundColor;
			public UIColor? DetailColor;
			public Uri? Uri;
			public Uri? BackgroundUri;
		}

		ExtraInfo OnImageInfo ()
		{
			return _extraInfo ??= new ExtraInfo();
		}
		
		// Uses the specified image (use this or ImageUri)
		public UIImage? Image {
			get => _extraInfo?.Image;
			set {
				OnImageInfo ().Image = value;
				OnImageInfo ().Uri = null;
			}
		}
		
		// Loads the image from the specified uri (use this or Image)
		public Uri? ImageUri {
			get => _extraInfo?.Uri;
			set {
				OnImageInfo ().Uri = value;
				OnImageInfo ().Image = null;
			}
		}
		
		// Background color for the cell (alternative: BackgroundUri)
		public UIColor? BackgroundColor {
			get => _extraInfo?.BackgroundColor;
			set {
				OnImageInfo ().BackgroundColor = value;
				OnImageInfo ().BackgroundUri = null;
			}
		}
		
		public UIColor? DetailColor {
			get => _extraInfo?.DetailColor;
			set => OnImageInfo ().DetailColor = value;
		}
		
		// Uri for a Background image (alternative: BackgroundColor)
		public Uri? BackgroundUri {
			get => _extraInfo?.BackgroundUri;
			set {
				OnImageInfo ().BackgroundUri = value;
				OnImageInfo ().BackgroundColor = null;
			}
		}
			
		protected virtual string GetKey (int style)
		{
			return SKey [style];
		}
		
		public override UITableViewCell GetCell (UITableView tv)
		{
			var key = GetKey ((int) Style);
			var cell = tv.DequeueReusableCell (key);
			if (cell == null){
				cell = new UITableViewCell (Style, key);
				cell.SelectionStyle = UITableViewCellSelectionStyle.Blue;
			}
			PrepareCell (cell);
			return cell;
		}
		
		protected void PrepareCell (UITableViewCell cell)
		{
			cell.Accessory = Accessory;
			var tl = cell.TextLabel;
			tl.Text = Caption;
			tl.TextAlignment = Alignment;
			tl.TextColor = TextColor ?? UIColor.Black;
			tl.Font = Font ?? UIFont.BoldSystemFontOfSize (17);
			tl.LineBreakMode = LineBreakMode;
			tl.Lines = Lines;	
			
			// The check is needed because the cell might have been recycled.
			if (cell.DetailTextLabel != null)
				cell.DetailTextLabel.Text = Value ?? "";
			
			if (_extraInfo == null){
				ClearBackground(cell);
			} else {
				var imgView = cell.ImageView;

				if (imgView != null) {
					UIImage? img;
					if (_extraInfo.Uri != null)
						img = ImageLoader.DefaultRequestImage (_extraInfo.Uri, this);
					else if (_extraInfo.Image != null)
						img = _extraInfo.Image;
					else 
						img = null;
					imgView.Image = img;
				}

				if (cell.DetailTextLabel != null)
					cell.DetailTextLabel.TextColor = _extraInfo.DetailColor ?? UIColor.Gray;
			}
				
			if (cell.DetailTextLabel != null){
				cell.DetailTextLabel.Lines = Lines;
				cell.DetailTextLabel.LineBreakMode = LineBreakMode;
				cell.DetailTextLabel.Font = SubtitleFont ?? UIFont.SystemFontOfSize (14);
				cell.DetailTextLabel.TextColor = (_extraInfo == null || _extraInfo.DetailColor == null) ? UIColor.Gray : _extraInfo.DetailColor;
			}
		}

		static void ClearBackground (UITableViewCell cell)
		{
			cell.BackgroundColor = UITableViewCell.Appearance.BackgroundColor;
			cell.TextLabel.BackgroundColor = UIColor.Clear;
		}

		void IColorizeBackground.WillDisplay (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
		{
			if (_extraInfo == null){
				ClearBackground(cell);
				return;
			}
			
			if (_extraInfo.BackgroundColor != null){
				cell.TextLabel.BackgroundColor = UIColor.Clear;
				cell.BackgroundColor = _extraInfo.BackgroundColor;
			} else if (_extraInfo.BackgroundUri != null){
				var img = ImageLoader.DefaultRequestImage (_extraInfo.BackgroundUri, this);
				cell.TextLabel.BackgroundColor = UIColor.Clear;
				cell.BackgroundColor = img == null ? UIColor.White : UIColor.FromPatternImage (img);
			} else
				ClearBackground(cell);
		}

		void IImageUpdated.UpdatedImage (Uri uri)
		{
			if (_extraInfo == null)
				return;
			var root = GetImmediateRootElement ();
			if (root == null || root.TableView == null || IndexPath == null)
				return;
			root.TableView.ReloadRows (new[] { IndexPath }, UITableViewRowAnimation.None);
		}	
		
		internal void AccessoryTap ()
		{
			NSAction? tapped = AccessoryTapped;
			if (tapped != null)
				tapped ();
		}
	}

#if __TVOS__
	internal static class Helper {

		static public CGSize StringSize (this string self, UIFont font)
		{
			using (var str = (NSString) self) {
				return str.GetSizeUsingAttributes (new UIStringAttributes ()
				{
					Font = font,
				});
			}
		}

		static public CGSize StringSize (this string self, UIFont font, CGSize constrainedToSize, UILineBreakMode lineBreakMode)
		{
			using (var str = (NSString) self) {
				return StringSize (str, font, constrainedToSize, lineBreakMode);
			}
		}

		static public CGSize StringSize (this NSString self, UIFont font, CGSize constrainedToSize, UILineBreakMode lineBreakMode)
		{
			return self.GetBoundingRect (constrainedToSize, NSStringDrawingOptions.UsesLineFragmentOrigin, new UIStringAttributes ()
			{
				Font = font,
				ParagraphStyle = new NSMutableParagraphStyle ()
				{
					LineBreakMode = lineBreakMode,
				},
			}, null).Size;
		}

		static public CGSize StringSize (this NSString self, UIFont font, float forWidth, UILineBreakMode lineBreakMode)
		{
			return StringSize (self, font, new CGSize (forWidth, nfloat.MaxValue), lineBreakMode);
		}

		static public void DrawString (this string self, CGRect rect, UIFont font)
		{
			using (var str = (NSString) self) {
				NSStringDrawing.DrawString (str, rect, new UIStringAttributes ()
				{
					Font = font,
				});
			}
		}

		static public void DrawString (this string self, CGPoint point, float width, UIFont font, UILineBreakMode lineBreakMode)
		{
			using (var str = (NSString) self) {
				NSStringDrawing.DrawString (str, point, new UIStringAttributes ()
				{
					Font = font,
					ParagraphStyle = new NSMutableParagraphStyle ()
					{
						LineBreakMode = lineBreakMode,
					},
				});
			}
		}

		static public void DrawString (this string self, CGRect rect, UIFont font, UILineBreakMode lineBreakMode, UITextAlignment alignment)
		{
			using (var str = (NSString) self) {
				NSStringDrawing.DrawString (str, rect, new UIStringAttributes ()
				{
					Font = font,
					ParagraphStyle = new NSMutableParagraphStyle ()
					{
						LineBreakMode = lineBreakMode,
						Alignment = alignment,
					},
				});
			}
		}
	}
#endif
	
	public class StyledMultilineElement : StyledStringElement, IElementSizing {
		public StyledMultilineElement (string caption) : base (caption) {}
		public StyledMultilineElement (string caption, string value) : base (caption, value) {}
		public StyledMultilineElement (string caption, NSAction tapped) : base (caption, tapped) {}
		public StyledMultilineElement (string caption, string value, UITableViewCellStyle style) : base (caption, value) 
		{ 
			this.Style = style;
		}

		public virtual nfloat GetHeight (UITableView tableView, NSIndexPath indexPath)
		{
			float margin = UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone ? 40f : 110f;
			CGSize maxSize = new CGSize (tableView.Bounds.Width - margin, float.MaxValue);
			
			if (this.Accessory != UITableViewCellAccessory.None)
				maxSize.Width -= 20;

			string? c = Caption;
			string? v = Value;
			// ensure the (multi-line) Value will be rendered inside the cell when no Caption is present
			if (String.IsNullOrEmpty (c))
				c = " ";

			var captionFont = Font ?? UIFont.BoldSystemFontOfSize (17);
			var height = c.StringSize (captionFont, maxSize, LineBreakMode).Height;
			
			if (!String.IsNullOrEmpty (v)) {
				var subtitleFont = SubtitleFont ?? UIFont.SystemFontOfSize (14);
				if (this.Style == UITableViewCellStyle.Subtitle) {
					height += v.StringSize (subtitleFont, maxSize, LineBreakMode).Height;
				} else {
					var vheight = v.StringSize (subtitleFont, maxSize, LineBreakMode).Height;
					if (vheight > height)
						height = vheight;
				}
			}
			
			return height + 10;
		}
	}
	
	public class ImageStringElement : StringElement {
		static readonly NSString SKey = new("ImageStringElement");
		readonly UIImage _image;
		public UITableViewCellAccessory Accessory { get; set; }
		
		public ImageStringElement (string caption, UIImage image) : base (caption)
		{
			_image = image;
			Accessory = UITableViewCellAccessory.None;
		}

		public ImageStringElement (string caption, string value, UIImage image) : base (caption, value)
		{
			_image = image;
			Accessory = UITableViewCellAccessory.None;
		}
		
		public ImageStringElement (string caption,  NSAction tapped, UIImage image) : base (caption, tapped)
		{
			_image = image;
			Accessory = UITableViewCellAccessory.None;
		}
		
		protected override NSString CellKey => SKey;

		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = tv.DequeueReusableCell (CellKey);
			if (cell == null){
				cell = new UITableViewCell (Value == null ? UITableViewCellStyle.Default : UITableViewCellStyle.Subtitle, CellKey);
				cell.SelectionStyle = UITableViewCellSelectionStyle.Blue;
			}
			
			cell.Accessory = Accessory;
			cell.TextLabel.Text = Caption;
			cell.TextLabel.TextAlignment = Alignment;
			
			cell.ImageView.Image = _image;
			
			// The check is needed because the cell might have been recycled.
			if (cell.DetailTextLabel != null)
				cell.DetailTextLabel.Text = Value == null ? "" : Value;
			
			return cell;
		}
		
	}
	
	/// <summary>
	///   This interface is implemented by Element classes that will have
	///   different heights
	/// </summary>
	public interface IElementSizing {
		nfloat GetHeight (UITableView tableView, NSIndexPath indexPath);
	}
	
	/// <summary>
	///   This interface is implemented by Elements that needs to update
	///   their cells Background properties just before they are displayed
	///   to the user.   This is an iOS 3 requirement to properly render
	///   a cell.
	/// </summary>
	public interface IColorizeBackground {
		void WillDisplay (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath);
	}
	
	[PublicAPI]
	public class MultilineElement : StringElement, IElementSizing {
		public MultilineElement (string caption) : base (caption)
		{
		}
		
		public MultilineElement (string caption, string? value) : base (caption, value)
		{
		}
		
		public MultilineElement (string caption, NSAction tapped) : base (caption, tapped)
		{
		}
		
		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = base.GetCell (tv);				
			var tl = cell.TextLabel;
			tl.LineBreakMode = UILineBreakMode.WordWrap;
			tl.Lines = 0;

			return cell;
		}
		
		public virtual nfloat GetHeight (UITableView tableView, NSIndexPath indexPath)
		{
			float margin = UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone ? 40f : 110f;
			CGSize size = new CGSize (tableView.Bounds.Width - margin, float.MaxValue);
			UIFont font = UIFont.BoldSystemFontOfSize (17);
			string? c = Caption;
			// ensure the (single-line) Value will be rendered inside the cell
			if (String.IsNullOrEmpty (c) && !String.IsNullOrEmpty (Value))
				c = " ";
			return c.StringSize (font, size, UILineBreakMode.WordWrap).Height + 10;
		}
	}
	
	public class RadioElement : StringElement {
		public string? Group;
		internal int RadioIdx;
		
		public RadioElement (string caption, string? group) : base (caption)
		{
			Group = group;
		}
				
		public RadioElement (string caption) : base (caption)
		{
		}

		public override UITableViewCell GetCell (UITableView tv)
		{
			Trace.Assert(Parent is { Parent: not null });

			var cell = base.GetCell (tv);			
			var root = (RootElement) Parent.Parent;
			
			if (root.Group is not RadioGroup group)
				throw new Exception ("The RootElement's Group is null or is not a RadioGroup");
			
			var selected = RadioIdx == group.Selected;
			cell.Accessory = selected ? UITableViewCellAccessory.Checkmark : UITableViewCellAccessory.None;

			return cell;
		}

		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath indexPath)
		{
			Trace.Assert(Parent is { Parent: not null });

			RootElement root = (RootElement) Parent.Parent;
			if (RadioIdx != root.RadioSelected){
				UITableViewCell? cell;
				var selectedIndex = root.PathForRadio (root.RadioSelected);
				if (selectedIndex != null) {
					cell = tableView.CellAt (selectedIndex);
					if (cell != null)
						cell.Accessory = UITableViewCellAccessory.None;
				}				
				cell = tableView.CellAt (indexPath);
				if (cell != null)
					cell.Accessory = UITableViewCellAccessory.Checkmark;
				root.RadioSelected = RadioIdx;
			}
			
			base.Selected (dvc, tableView, indexPath);
		}
	}
	
	public class CheckboxElement : StringElement {
		public new bool Value;
		public string? Group;
		
		public CheckboxElement (string caption) : base (caption) {}
		public CheckboxElement (string caption, bool value) : base (caption)
		{
			Value = value;
		}
		
		public CheckboxElement (string caption, bool value, string group) : this (caption, value)
		{
			Group = group;
		}
		
		UITableViewCell ConfigCell (UITableViewCell cell)
		{
			cell.Accessory = Value ? UITableViewCellAccessory.Checkmark : UITableViewCellAccessory.None;
			return cell;
		}
		
		public override UITableViewCell GetCell (UITableView tv)
		{
			return  ConfigCell (base.GetCell (tv));
		}
		
		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
		{
			Value = !Value;
			var cell = tableView.CellAt (path);
			Trace.Assert(cell is not null);
			ConfigCell (cell);
			base.Selected (dvc, tableView, path);
		}

	}
	
	public class ImageElement : Element {
		public UIImage Value;
		static readonly CGRect Rect = new(0, 0, DimX, DimY);
		static readonly NSString Ikey = new("ImageElement");
		UIImage? _scaled;

		// There's no UIImagePickerController in tvOS (and I couldn't find any suitable replacement either).
#if !__TVOS__
		UIPopoverController? _popover;
		
		// Apple leaks this one, so share across all.
		static UIImagePickerController? _picker;
#endif // !__TVOS__
		
		// Height for rows
		const int DimX = 48;
		const int DimY = 43;
		
		// radius for rounding
		const int Rad = 10;
		
		static UIImage MakeEmpty ()
		{
			using var cs = CGColorSpace.CreateDeviceRGB ();
			using var bit = new CGBitmapContext (IntPtr.Zero, DimX, DimY, 8, 0, cs, CGImageAlphaInfo.PremultipliedFirst);
			bit.SetStrokeColor (1, 0, 0, 0.5f);
			bit.FillRect (new CGRect (0, 0, DimX, DimY));
			var image = bit.ToImage();
			Trace.Assert(image is not null);
			return UIImage.FromImage (image);
		}

		static UIImage? Scale (UIImage source)
		{
			if (source.CGImage is null)
				return null;
			
			UIGraphics.BeginImageContext (new CGSize (DimX, DimY));
			var ctx = UIGraphics.GetCurrentContext ();
		
			var img = source.CGImage;
			ctx.TranslateCTM (0, DimY);
			if (img.Width > img.Height)
				ctx.ScaleCTM (1, -img.Width/DimY);
			else
				ctx.ScaleCTM ((nfloat)img.Height/DimX, -1);

			ctx.DrawImage (Rect, source.CGImage);
			
			var ret = UIGraphics.GetImageFromCurrentImageContext ();
			UIGraphics.EndImageContext ();
			return ret;
		}
		
		public ImageElement (UIImage? image) : base ("")
		{
			if (image == null){
				Value = MakeEmpty ();
				_scaled = Value;
			} else {
				Value = image;			
				_scaled = Scale (Value);
			}
		}
		
		protected override NSString CellKey => Ikey;

		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = tv.DequeueReusableCell (CellKey) ?? new UITableViewCell (UITableViewCellStyle.Default, CellKey);

			if (_scaled == null)
				return cell;
			
			var pSection = Parent as Section;
			Trace.Assert(pSection is not null);
			bool roundTop = pSection.Elements [0] == this;
			bool roundBottom = pSection.Elements [^1] == this;

			using var cs = CGColorSpace.CreateDeviceRGB ();
			using var bit = new CGBitmapContext (IntPtr.Zero, DimX, DimY, 8, 0, cs, CGImageAlphaInfo.PremultipliedFirst);
			// Clipping path for the image, different on top, middle and bottom.
			if (roundBottom){
				bit.AddArc (Rad, Rad, Rad, (float) Math.PI, (float) (3*Math.PI/2), false);
			} else {
				bit.MoveTo (0, Rad);
				bit.AddLineToPoint (0, 0);
			}
			bit.AddLineToPoint (DimX, 0);
			bit.AddLineToPoint (DimX, DimY);
					
			if (roundTop){
				bit.AddArc (Rad, DimY-Rad, Rad, (float) (Math.PI/2), (float) Math.PI, false);
				bit.AddLineToPoint (0, Rad);
			} else {
				bit.AddLineToPoint (0, DimY);
			}
			bit.Clip ();
			bit.DrawImage (Rect, _scaled.CGImage);
			var image = bit.ToImage();
			cell.ImageView.Image = image != null ? UIImage.FromImage (image) : null;

			return cell;
		}
		
		protected override void Dispose (bool disposing)
		{
			if (disposing){
				if (_scaled != null){
					_scaled.Dispose ();
					Value.Dispose ();
					_scaled = null;
					// Value = null;
				}
			}
			base.Dispose (disposing);
		}

#if !__TVOS__
		class MyDelegate : UIImagePickerControllerDelegate {
			readonly ImageElement _container;
			readonly UITableView _table;
			readonly NSIndexPath _path;
			
			public MyDelegate (ImageElement container, UITableView table, NSIndexPath path)
			{
				_container = container;
				_table = table;
				_path = path;
			}
			
#if !NET
			public override void FinishedPickingImage (UIImagePickerController picker, UIImage image, NSDictionary editingInfo)
			{
				container.Picked (image);
				table.ReloadRows (new NSIndexPath [] { path }, UITableViewRowAnimation.None);
			}
#else
			public override void FinishedPickingMedia (UIImagePickerController picker, NSDictionary info)
			{
				var image = (UIImage) (info [UIImagePickerController.OriginalImage] ?? info [UIImagePickerController.EditedImage]);
				_container.Picked (image);
				_table.ReloadRows (new[] { _path }, UITableViewRowAnimation.None);
			}
#endif
		}
		
		void Picked (UIImage image)
		{
			Value = image;
			_scaled = Scale (image);
			_currentController?.DismissModalViewController (true);
		}
		
		UIViewController? _currentController;
		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
		{
			_picker ??= new UIImagePickerController();
			_picker.Delegate = new MyDelegate (this, tableView, path);
			
			switch (UIDevice.CurrentDevice.UserInterfaceIdiom){
			case UIUserInterfaceIdiom.Pad:
				CGRect useRect;
				_popover = new UIPopoverController (_picker);
				var cell = tableView.CellAt (path);
				useRect = cell?.Frame ?? Rect;
				Trace.Assert(dvc.View is not null);
				_popover.PresentFromRect (useRect, dvc.View, UIPopoverArrowDirection.Any, true);
				break;
				
			default:
			case UIUserInterfaceIdiom.Phone:
				dvc.ActivateController (_picker);
				break;
			}
			_currentController = dvc;
		}
#endif // !__TVOS__
	}
	
	/// <summary>
	/// An element that can be used to enter text.
	/// </summary>
	/// <remarks>
	/// This element can be used to enter text both regular and password protected entries. 
	///     
	/// The Text fields in a given section are aligned with each other.
	/// </remarks>
	[PublicAPI]
	public class EntryElement : Element {
		/// <summary>
		///   The value of the EntryElement
		/// </summary>
		public string? Value { 
			get {
				if (_entry == null)
					return Val;
				var newValue = _entry.Text;
				if (newValue == Val)
					return Val;
				Val = newValue;

				if (Changed != null)
					Changed (this, EventArgs.Empty);
				return Val;
			}
			set {
				Val = value;
				if (_entry != null)
					_entry.Text = value;
			}
		}
		protected string? Val;

		/// <summary>
		/// The key used for reusable UITableViewCells.
		/// </summary>
		static readonly NSString EntryKeyS = new("EntryElement");
		protected virtual NSString EntryKey => EntryKeyS;

		/// <summary>
		/// The type of keyboard used for input, you can change
		/// this to use this for numeric input, email addressed,
		/// urls, phones.
		/// </summary>
		public UIKeyboardType KeyboardType {
			get => _keyboardType;
			set {
				_keyboardType = value;
				if (_entry != null)
					_entry.KeyboardType = value;
			}
		}
		
		/// <summary>
		/// The type of Return Key that is displayed on the
		/// keyboard, you can change this to use this for
		/// Done, Return, Save, etc. keys on the keyboard
		/// </summary>
		public UIReturnKeyType? ReturnKeyType {
			get => _returnKeyType;
			set {
				_returnKeyType = value;
				if (_entry != null && _returnKeyType.HasValue)
					_entry.ReturnKeyType = _returnKeyType.Value;
			}
		}

		/// <summary>
		/// The default value for this property is <c>false</c>. If you set it to <c>true</c>, the keyboard disables the return key when the text entry area contains no text. As soon as the user enters any text, the return key is automatically enabled.
		/// </summary>
		public bool EnablesReturnKeyAutomatically {
			get => _enablesReturnKeyAutomatically;
			set {
				_enablesReturnKeyAutomatically = value;
				if (_entry != null)
					_entry.EnablesReturnKeyAutomatically = value;
			}
		}
		
		public UITextAutocapitalizationType AutocapitalizationType {
			get => _autocapitalizationType;
			set { 
				_autocapitalizationType = value;
				if (_entry != null)
					_entry.AutocapitalizationType = value;
			}
		}
		
		public UITextAutocorrectionType AutocorrectionType { 
			get => _autocorrectionType;
			set { 
				_autocorrectionType = value;
				if (_entry != null)
					this._autocorrectionType = value;
			}
		}
		
		public UITextFieldViewMode ClearButtonMode { 
			get => _clearButtonMode;
			set { 
				_clearButtonMode = value;
				if (_entry != null)
					_entry.ClearButtonMode = value;
			}
		}

		public UITextAlignment TextAlignment {
			get => _textAlignment;
			set{
				_textAlignment = value;
				if (_entry != null) {
					_entry.TextAlignment = _textAlignment;
				}
			}
		}

		public bool AlignEntryWithAllSections { get; set; }

		public bool NotifyChangedOnKeyStroke { get; set; }

		UITextAlignment _textAlignment = UITextAlignment.Left;
		UIKeyboardType _keyboardType = UIKeyboardType.Default;
		UIReturnKeyType? _returnKeyType;
		bool _enablesReturnKeyAutomatically;
		UITextAutocapitalizationType _autocapitalizationType = UITextAutocapitalizationType.Sentences;
		UITextAutocorrectionType _autocorrectionType = UITextAutocorrectionType.Default;
		UITextFieldViewMode _clearButtonMode = UITextFieldViewMode.Never;
		readonly bool _isPassword;
		bool _becomeResponder;
		UITextField? _entry;
		readonly string? _placeholder;
		static readonly UIFont Font = UIFont.BoldSystemFontOfSize (17);

		public event EventHandler? Changed;
		public event Func<bool>? ShouldReturn;
		public EventHandler? EntryStarted {get;set;}
		public EventHandler? EntryEnded {get;set;}
		/// <summary>
		/// Constructs an EntryElement with the given caption, placeholder and initial value.
		/// </summary>
		/// <param name="caption">
		/// The caption to use
		/// </param>
		/// <param name="placeholder">
		/// Placeholder to display when no value is set.
		/// </param>
		/// <param name="value">
		/// Initial value.
		/// </param>
		public EntryElement (string caption, string? placeholder, string? value) : base (caption)
		{ 
			Value = value;
			this._placeholder = placeholder;
		}
		
		/// <summary>
		/// Constructs an EntryElement for password entry with the given caption, placeholder and initial value.
		/// </summary>
		/// <param name="caption">
		/// The caption to use.
		/// </param>
		/// <param name="placeholder">
		/// Placeholder to display when no value is set.
		/// </param>
		/// <param name="value">
		/// Initial value.
		/// </param>
		/// <param name="isPassword">
		/// True if this should be used to enter a password.
		/// </param>
		public EntryElement (string caption, string? placeholder, string? value, bool isPassword) : base (caption)
		{
			Value = value;
			_isPassword = isPassword;
			_placeholder = placeholder;
		}

		public override string Summary ()
		{
			return Value ?? "";
		}

		// 
		// Computes the X position for the entry by aligning all the entries in the Section
		//
		CGSize ComputeEntryPosition (UITableView tv, UITableViewCell cell)
		{
			Trace.Assert(Parent is not null && Parent.Parent is not null);
			var rootElement = Parent.Parent as RootElement;
			Trace.Assert(rootElement is not null);
			
			nfloat maxWidth = -15; // If all EntryElements have a null Caption, align UITextField with the Caption offset of normal cells (at 10px).
			nfloat maxHeight = Font.LineHeight;

			// Determine if we should calculate across all sections or just the current section.
			
			var sections = AlignEntryWithAllSections ? rootElement.Sections : new[] {Parent as Section}.AsEnumerable();

			foreach (Section? s in sections) {

				foreach (var e in s != null ? s.Elements: []) {
					if (e is EntryElement ee
					    && !string.IsNullOrEmpty(ee.Caption)) {
								
						var size = ee.Caption.StringSize (Font);

						maxWidth = (nfloat) Math.Max (size.Width, maxWidth);
						maxHeight = (nfloat) Math.Max (size.Height, maxHeight);
					}
				}
			}

			return new CGSize (25 + (nfloat) Math.Min (maxWidth, 160), maxHeight);
		}

		protected virtual UITextField CreateTextField (CGRect frame)
		{
			return new UITextField (frame) {
				AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleLeftMargin,
				Placeholder = _placeholder ?? "",
				SecureTextEntry = _isPassword,
				Text = Value ?? "",
				Tag = 1,
				TextAlignment = _textAlignment,
				ClearButtonMode = ClearButtonMode
			};
		}

		static readonly NSString PasswordKey = new("EntryElement+Password");
		static readonly NSString EntryElementKey = new("EntryElement");
		
		protected override NSString CellKey => _isPassword ? PasswordKey : EntryElementKey;

		UITableViewCell? _cell;
		public override UITableViewCell GetCell (UITableView tv)
		{
			if (_cell == null) {
				_cell = new UITableViewCell (UITableViewCellStyle.Default, CellKey);
				_cell.SelectionStyle = UITableViewCellSelectionStyle.None;
				_cell.TextLabel.Font = Font;

			} 
			_cell.TextLabel.Text = Caption;

			var offset = (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone) ? 20 : 90;
			_cell.Frame = new CGRect(_cell.Frame.X, _cell.Frame.Y, tv.Frame.Width-offset, _cell.Frame.Height);
			CGSize size = ComputeEntryPosition (tv, _cell);
			nfloat yOffset = (_cell.ContentView.Bounds.Height - size.Height) / 2 - 1;
			nfloat width = _cell.ContentView.Bounds.Width - size.Width;
			if (_textAlignment == UITextAlignment.Right) {
				// Add padding if right aligned
				width -= 10;
			}
#if __TVOS__
			var entryFrame = new CGRect (size.Width, yOffset, width, size.Height + 20 /* FIXME: figure out something better than adding a magic number */);
#else
			var entryFrame = new CGRect (size.Width, yOffset, width, size.Height);
#endif

			if (_entry == null) {
				_entry = CreateTextField (entryFrame);
				_entry.EditingChanged += delegate {
					if(NotifyChangedOnKeyStroke) {
						FetchValue ();
					}
				};
				_entry.ValueChanged += delegate {
					FetchValue ();
				};
				_entry.Ended += delegate {					
					FetchValue ();
					if (EntryEnded != null) {
						EntryEnded (this, EventArgs.Empty);
					}
				};
				_entry.ShouldReturn += delegate {
					
					if (ShouldReturn != null)
						return ShouldReturn ();
					
					RootElement? root = GetImmediateRootElement ();
					EntryElement? focus = null;
					
					if (root == null)
						return true;
					
					foreach (var s in root.Sections) {
						foreach (var e in s.Elements) {
							if (e == this) {
								focus = this;
							} else if (focus != null && e is EntryElement element) {
								focus = element;
								break;
							}
						}
						
						if (focus != null && focus != this)
							break;
					}
					
					if (focus != this)
						focus?.BecomeFirstResponder (true);
					else 
						focus.ResignFirstResponder (true);
					
					return true;
				};
				_entry.Started += delegate {
					EntryElement? self = null;
					
					if (EntryStarted != null) {
						EntryStarted (this, EventArgs.Empty);
					}
					
					if (!_returnKeyType.HasValue) {
						var returnType = UIReturnKeyType.Default;

						foreach (var e in Parent is Section parent ? parent.Elements : []) {
							if (e == this)
								self = this;
							else if (self != null && e is EntryElement)
								returnType = UIReturnKeyType.Next;
						}
						_entry.ReturnKeyType = returnType;
					} else
						_entry.ReturnKeyType = _returnKeyType.Value;

					var indexPath = IndexPath;
					if (indexPath is not null) 
						tv.ScrollToRow (indexPath, UITableViewScrollPosition.Middle, true);
				};
				_cell.ContentView.AddSubview (_entry);
			}

			if (_becomeResponder){
				_entry.BecomeFirstResponder ();
				_becomeResponder = false;
			}
			_entry.KeyboardType = KeyboardType;
			_entry.EnablesReturnKeyAutomatically = EnablesReturnKeyAutomatically;
			_entry.AutocapitalizationType = AutocapitalizationType;
			_entry.AutocorrectionType = AutocorrectionType;

			return _cell;
		}
		
		/// <summary>
		///  Copies the value from the UITextField in the EntryElement to the
		///  Value property and raises the Changed event if necessary.
		/// </summary>
		public void FetchValue ()
		{
			if (_entry == null)
				return;

			var newValue = _entry.Text;
			if (newValue == Value)
				return;
			
			Value = newValue;
			
			if (Changed != null)
				Changed (this, EventArgs.Empty);
		}
		
		protected override void Dispose (bool disposing)
		{
			if (disposing){
				if (_entry != null){
					_entry.Dispose ();
					_entry = null;
				}
			}
		}

		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath indexPath)
		{
			BecomeFirstResponder(true);
			tableView.DeselectRow (indexPath, true);
		}
		
		public override bool Matches (string text)
		{
			return (Value != null && Value.Contains(text, StringComparison.CurrentCultureIgnoreCase)) || base.Matches (text);
		}
		
		/// <summary>
		/// Makes this cell the first responder (get the focus)
		/// </summary>
		/// <param name="animated">
		/// Whether scrolling to the location of this cell should be animated
		/// </param>
		public virtual void BecomeFirstResponder (bool animated)
		{
			_becomeResponder = true;
			var tv = GetContainerTableView ();
			if (tv == null)
				return;
			var indexPath = IndexPath;
			if (indexPath is not null)
				tv.ScrollToRow (indexPath, UITableViewScrollPosition.Middle, animated);
			if (_entry != null){
				_entry.BecomeFirstResponder ();
				_becomeResponder = false;
			}
		}

		public virtual void ResignFirstResponder (bool animated)
		{
			_becomeResponder = false;
			var tv = GetContainerTableView ();
			if (tv == null)
				return;
			var indexPath = IndexPath;
			if (indexPath is not null)
				tv.ScrollToRow (indexPath, UITableViewScrollPosition.Middle, animated);
			if (_entry != null)
				_entry.ResignFirstResponder ();
		}
	}
	
	[PublicAPI]
	public class DateTimeElement : StringElement {
		public DateTime DateValue;
		// There's no UIDatePicker for tvOS, so this is a read-only element for now
#if !__TVOS__
		public UIDatePicker? DatePicker;
#endif
		public int MinuteInterval = 1;
#pragma warning disable 67 // The event 'X' is never used
		public event Action<DateTimeElement>? DateSelected;
#pragma warning restore 67
		public UIColor BackgroundColor = (UIDevice.CurrentDevice.CheckSystemVersion (7, 0)) ? UIColor.White : UIColor.Black;
		
		protected internal NSDateFormatter Fmt = new() {
			DateStyle = NSDateFormatterStyle.Short
		};

		public DateTimeElement (string caption, DateTime date) : base (caption)
		{
			DateValue = date;
			Value = FormatDate (date);
		}	
		
		public override UITableViewCell GetCell (UITableView tv)
		{
			Value = FormatDate (DateValue);
			var cell = base.GetCell (tv);
			cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
            cell.SelectionStyle = UITableViewCellSelectionStyle.Blue;
			return cell;
		}
 
		protected override void Dispose (bool disposing)
		{
			base.Dispose (disposing);
			if (disposing){
#if !__TVOS__
				if (DatePicker != null){
					DatePicker.Dispose ();
					DatePicker = null;
				}
#endif // !__TVOS__
			}
		}
		
		protected static DateTime GetDateWithKind (DateTime dt)
		{
			return dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind (dt, DateTimeKind.Local) : dt;
		}
		
		public virtual string FormatDate (DateTime dt)
		{
			dt = GetDateWithKind(dt);
			return Fmt.ToString ((NSDate) dt) + " " + dt.ToLocalTime ().ToShortTimeString ();
		}
		
#if !__TVOS__
		public virtual UIDatePicker CreatePicker ()
		{
			var picker = new UIDatePicker (CGRect.Empty){
				AutoresizingMask = UIViewAutoresizing.FlexibleMargins,
				Mode = UIDatePickerMode.DateAndTime,
				Date = (NSDate)GetDateWithKind(DateValue),
				MinuteInterval = MinuteInterval
			};
			return picker;
		}
		                                                                                                                                                                                                                                                            
		class MyViewController : UIViewController {
			readonly DateTimeElement _container;
			
			public MyViewController (DateTimeElement container)
			{
				_container = container;
			}
			
			public override void ViewWillDisappear (bool animated)
			{
				base.ViewWillDisappear (animated);

				if (_container.DatePicker is null) 
					return;
				_container.DateValue = (DateTime) _container.DatePicker.Date;
				_container.DateSelected?.Invoke (_container);
			}
			
			public override void DidRotate (UIInterfaceOrientation fromInterfaceOrientation)
			{
				base.DidRotate (fromInterfaceOrientation);
				
				if (_container.DatePicker is not null && this.View is not null)
					_container.DatePicker.Center = this.View.Center;
			}
			
			public bool Autorotate { get; init; }
			
			public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
			{
				return Autorotate;
			}
		}
		
		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
		{
			var vc = new MyViewController (this) {
				Autorotate = dvc.Autorotate
			};
			DatePicker = CreatePicker ();
			        
			Trace.Assert(vc.View is not null);
			vc.View.BackgroundColor = BackgroundColor;
			vc.View.AddSubview (DatePicker);
			dvc.ActivateController (vc);

			DatePicker.Center = vc.View.Center;
		}
#endif // !__TVOS__                                                                                                                     
	}
	
	public class DateElement : DateTimeElement {
		public DateElement (string caption, DateTime date) : base (caption, date)
		{
			Fmt.DateStyle = NSDateFormatterStyle.Medium;
		}
		
		public override string FormatDate (DateTime dt)
		{
			return Fmt.ToString ((NSDate)GetDateWithKind(dt));
		}
		
#if !__TVOS__
		public override UIDatePicker CreatePicker ()
		{
			var picker = base.CreatePicker ();
			picker.Mode = UIDatePickerMode.Date;
			return picker;
		}
#endif // !__TVOS__
	}
	
	public class TimeElement : DateTimeElement {
		public TimeElement (string caption, DateTime date) : base (caption, date)
		{
		}
		
		public override string FormatDate (DateTime dt)
		{
			return GetDateWithKind(dt).ToLocalTime ().ToShortTimeString ();
		}
		
#if !__TVOS__
		public override UIDatePicker CreatePicker ()
		{
			var picker = base.CreatePicker ();
			picker.Mode = UIDatePickerMode.Time;
			picker.MinuteInterval = MinuteInterval;
			return picker;
		}
#endif // !__TVOS__
	}
	
	/// <summary>
	///   This element can be used to insert an arbitrary UIView
	/// </summary>
	/// <remarks>
	///   There is no cell reuse here as we have a 1:1 mapping
	///   in this case from the UIViewElement to the cell that
	///   holds our view.
	/// </remarks>
	[PublicAPI]
	public class UIViewElement : Element, IElementSizing {
		static int _count;
		public UIView ContainerView;
		readonly NSString _key;
		protected UIView? View;
		public CellFlags Flags;
		UIEdgeInsets _insets;

		public UIEdgeInsets Insets { 
			get => _insets;
			set {
				Trace.Assert(View is not null);
				var viewFrame = View.Frame;
				var dx = value.Left - _insets.Left;
				var dy = value.Top - _insets.Top;
				var ow = _insets.Left + _insets.Right;
				var oh = _insets.Top + _insets.Bottom;
				var w = value.Left + value.Right;
				var h = value.Top + value.Bottom;

				ContainerView.Frame = new CGRect (0, 0, ContainerView.Frame.Width + w - ow, ContainerView.Frame.Height + h -oh);
				viewFrame.X += dx;
				viewFrame.Y += dy;
				View.Frame = viewFrame;

				_insets = value;

				// Height changed, notify UITableView
				if (dy != 0 || h != oh)
					GetContainerTableView ()?.ReloadData ();
				
			}
		}

		[Flags]
		public enum CellFlags {
			Transparent = 1,
			DisableSelection = 2
		}


		/// <summary>
		///   Constructor
		/// </summary>
		/// <param name="caption">
		/// The caption, only used for RootElements that might want to summarize results
		/// </param>
		/// <param name="view">
		/// The view to display
		/// </param>
		/// <param name="transparent">
		/// If this is set, then the view is responsible for painting the entire area,
		/// otherwise the default cell paint code will be used.
		/// </param>
		/// <param name="insets"></param>
		public UIViewElement (string? caption, UIView view, bool transparent, UIEdgeInsets insets) : base (caption) 
		{
			this._insets = insets;
			var oFrame = view.Frame;
			var frame = oFrame;
			frame.Width += insets.Left + insets.Right;
			frame.Height += insets.Top + insets.Bottom;

			ContainerView = new UIView (frame);
			if ((Flags & CellFlags.Transparent) != 0)
				ContainerView.BackgroundColor = UIColor.Clear;

			if (insets.Left != 0 || insets.Top != 0)
				view.Frame = new CGRect (insets.Left + frame.X, insets.Top + frame.Y, frame.Width, frame.Height);

			ContainerView.AddSubview (view);
			View = view;
			Flags = transparent ? CellFlags.Transparent : 0;
			_key = new NSString ("UIViewElement" + _count++);
		}
		
		public UIViewElement (string? caption, UIView view, bool transparent) : this (caption, view, transparent, UIEdgeInsets.Zero)
		{
		}

		protected override NSString CellKey => _key;

		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = tv.DequeueReusableCell (CellKey);
			if (cell == null){
				cell = new UITableViewCell (UITableViewCellStyle.Default, CellKey);
				if ((Flags & CellFlags.Transparent) != 0){
					cell.BackgroundColor = UIColor.Clear;
					
					// 
					// This trick is necessary to keep the background clear, otherwise
					// it gets painted as black
					//
					cell.BackgroundView = new UIView (CGRect.Empty) { 
						BackgroundColor = UIColor.Clear 
					};
				}
				if ((Flags & CellFlags.DisableSelection) != 0)
					cell.SelectionStyle = UITableViewCellSelectionStyle.None;

				if (Caption != null)
					cell.TextLabel.Text = Caption;
				cell.ContentView.AddSubview (ContainerView);
			} 
			return cell;
		}
		
		public nfloat GetHeight (UITableView tableView, NSIndexPath indexPath)
		{
			return ContainerView.Bounds.Height+1;
		}
		
		protected override void Dispose (bool disposing)
		{
			base.Dispose (disposing);
			if (disposing){
				if (View != null){
					View.Dispose ();
					View = null;
				}
			}
		}
	}
	
	/// <summary>
	/// Sections contain individual Element instances that are rendered by MonoTouch.Dialog
	/// </summary>
	/// <remarks>
	/// Sections are used to group elements in the screen and they are the
	/// only valid direct child of the RootElement.    Sections can contain
	/// any of the standard elements, including new RootElements.
	/// 
	/// RootElements embedded in a section are used to navigate to a new
	/// deeper level.
	/// 
	/// You can assign a header and a footer either as strings (Header and Footer)
	/// properties, or as UIViews to be shown (HeaderView and FooterView).   Internally
	/// this uses the same storage, so you can only show one or the other.
	/// </remarks>
	[PublicAPI]
	public class Section : Element, IEnumerable
	{
		object? _header;
		object? _footer;
		public List<Element> Elements = new List<Element> ();
				
		// X corresponds to the alignment, Y to the height of the password
		public CGSize EntryAlignment;
		
		/// <summary>
		///  Constructs a Section without header or footers.
		/// </summary>
		public Section () : base (null) {}
		
		/// <summary>
		///  Constructs a Section with the specified header
		/// </summary>
		/// <param name="caption">
		/// The header to display
		/// </param>
		public Section (string caption) : base (caption)
		{
		}
		
		/// <summary>
		/// Constructs a Section with a header and a footer
		/// </summary>
		/// <param name="caption">
		/// The caption to display (or null to not display a caption)
		/// </param>
		/// <param name="footer">
		/// The footer to display.
		/// </param>
		public Section (string? caption, string? footer) : base (caption)
		{
			Footer = footer;
		}

		public Section (UIView header) : base (null)
		{
			HeaderView = header;
		}
		
		public Section (UIView header, UIView footer) : base (null)
		{
			HeaderView = header;
			FooterView = footer;
		}
		
		/// <summary>
		///    The section header, as a string
		/// </summary>
		public string? Header {
			get => _header as string;
			set => _header = value;
		}
		
		/// <summary>
		/// The section footer, as a string.
		/// </summary>
		public string? Footer {
			get => _footer as string;
			set => _footer = value;
		}
		
		/// <summary>
		/// The section's header view.  
		/// </summary>
		public UIView? HeaderView {
			get => _header as UIView;
			set => _header = value;
		}
		
		/// <summary>
		/// The section's footer view.
		/// </summary>
		public UIView? FooterView {
			get => _footer as UIView;
			set => _footer = value;
		}
		
		/// <summary>
		/// Adds a new child Element to the Section
		/// </summary>
		/// <param name="element">
		/// An element to add to the section.
		/// </param>
		public void Add (Element? element)
		{
			if (element == null)
				return;
			
			Trace.Assert(Elements is not null);
			
			Elements.Add (element);
			element.Parent = this;
			
			if (Parent != null)
				InsertVisual (Elements.Count-1, UITableViewRowAnimation.None, 1);
		}

		/// <summary>
		/// Adds a new child RootElement to the Section. This only exists to fix a compiler breakage when the mono 3.0 mcs is used.
		/// </summary>
		/// <param name="element">
		/// An element to add to the section.
		/// </param>
		public void Add (RootElement element)
		{
			Add ((Element)element);
		}

		/// <summary>
		///    Add version that can be used with LINQ
		/// </summary>
		/// <param name="elements">
		/// An enumerable list that can be produced by something like:
		///    from x in ... select (Element) new MyElement (...)
		/// </param>
		public int AddAll (IEnumerable<Element> elements)
		{
			int count = 0;
			foreach (var e in elements){
				Add (e);
				count++;
			}
			return count;
		}
		
		/// <summary>
		///    This method is being obsoleted, use AddAll to add an `IEnumerable<Element>` instead.
		/// </summary>
		[Obsolete ("Please use AddAll since this version will not work in future versions of MonoTouch when we introduce 4.0 covariance")]
		public int Add (IEnumerable<Element> elements)
		{
			return AddAll (elements);
		}
		
		/// <summary>
		/// Use to add a UIView to a section, it makes the section opaque, to
		/// get a transparent one, you must manually call UIViewElement
		/// </summary>
		public void Add (UIView? view)
		{
			if (view == null)
				return;
			Add (new UIViewElement (null, view, false));
		}

		/// <summary>
		///   Adds the UIViews to the section.
		/// </summary>
		/// <param name="views">
		/// An enumerable list that can be produced by something like:
		///    from x in ... select (UIView) new UIFoo ();
		/// </param>
		public void Add (IEnumerable<UIView> views)
		{
			foreach (var v in views)
				Add (v);
		}
		
		/// <summary>
		/// Inserts a series of elements into the Section using the specified animation
		/// </summary>
		/// <param name="idx">
		/// The index where the elements are inserted
		/// </param>
		/// <param name="anim">
		/// The animation to use
		/// </param>
		/// <param name="newElements">
		/// A series of elements.
		/// </param>
		public void Insert (int idx, UITableViewRowAnimation anim, params Element []? newElements)
		{
			if (newElements == null)
				return;
	
			Trace.Assert(Elements is not null);
			
			int pos = idx;
			foreach (var e in newElements){
				Elements.Insert (pos++, e);
				e.Parent = this;
			}

			if (Parent is RootElement { TableView: not null } root){
				if (anim == UITableViewRowAnimation.None)
					root.TableView.ReloadData ();
				else
					InsertVisual (idx, anim, newElements.Length);
			}
		}

		public int Insert (int idx, UITableViewRowAnimation anim, IEnumerable<Element>? newElements)
		{
			if (newElements == null)
				return 0;

			int pos = idx;
			int count = 0;
			foreach (var e in newElements){
				Elements.Insert (pos++, e);
				e.Parent = this;
				count++;
			}

			if (Parent is RootElement { TableView: not null } root){				
				if (anim == UITableViewRowAnimation.None)
					root.TableView.ReloadData ();
				else
					InsertVisual (idx, anim, pos-idx);
			}
			return count;
		}

		/// <summary>
		/// Inserts a single RootElement into the Section using the specified animation
		/// </summary>
		/// <param name="idx">
		/// The index where the elements are inserted
		/// </param>
		/// <param name="anim">
		/// The animation to use
		/// </param>
		/// <param name="newElement">
		/// A series of elements.
		/// </param>
		public void Insert (int idx, UITableViewRowAnimation anim, RootElement newElement)
		{
			Insert (idx, anim, (Element) newElement);
		}

		void InsertVisual (int idx, UITableViewRowAnimation anim, int count)
		{
			var root = Parent as RootElement;
			
			if (root == null || root.TableView == null)
				return;
			
			int sIdX = root.IndexOf (this);
			var paths = new NSIndexPath [count];
			for (int i = 0; i < count; i++)
				paths [i] = NSIndexPath.FromRowSection (idx+i, sIdX);
			
			root.TableView.InsertRows (paths, anim);
		}
		
		public void Insert (int index, params Element [] newElements)
		{
			Insert (index, UITableViewRowAnimation.None, newElements);
		}
		
		public void Remove (Element? e)
		{
			if (e == null)
				return;
			for (int i = Elements.Count; i > 0;){
				i--;
				if (Elements [i] == e){
					RemoveRange (i, 1);
					return;
				}
			}
		}
		
		public void Remove (int idx)
		{
			RemoveRange (idx, 1);
		}
		
		/// <summary>
		/// Removes a range of elements from the Section
		/// </summary>
		/// <param name="start">
		/// Starting position
		/// </param>
		/// <param name="count">
		/// Number of elements to remove from the section
		/// </param>
		public void RemoveRange (int start, int count)
		{
			RemoveRange (start, count, UITableViewRowAnimation.Fade);
		}

		/// <summary>
		/// Remove a range of elements from the section with the given animation
		/// </summary>
		/// <param name="start">
		/// Starting position
		/// </param>
		/// <param name="count">
		/// Number of elements to remove form the section
		/// </param>
		/// <param name="anim">
		/// The animation to use while removing the elements
		/// </param>
		public void RemoveRange (int start, int count, UITableViewRowAnimation anim)
		{
			if (start < 0 || start >= Elements.Count)
				return;
			if (count == 0)
				return;
			
			var root = Parent as RootElement;
			
			if (start+count > Elements.Count)
				count = Elements.Count-start;
			
			Elements.RemoveRange (start, count);
			
			if (root == null || root.TableView == null)
				return;
			
			int sIdX = root.IndexOf (this);
			var paths = new NSIndexPath [count];
			for (int i = 0; i < count; i++)
				paths [i] = NSIndexPath.FromRowSection (start+i, sIdX);
			root.TableView.DeleteRows (paths, anim);
		}
		
		/// <summary>
		/// Enumerator to get all the elements in the Section.
		/// </summary>
		/// <returns>
		/// A <see cref="IEnumerator"/>
		/// </returns>
		public IEnumerator GetEnumerator ()
		{
			foreach (var e in Elements)
				yield return e;
		}

		public int Count => Elements.Count;

		public Element this [int idx] => Elements [idx];

		public void Clear ()
		{
			foreach (var e in Elements)
				e.Dispose ();
			Elements = new List<Element> ();

			var root = Parent as RootElement;
			root?.TableView?.ReloadData ();
		}
				
		protected override void Dispose (bool disposing)
		{
			if (disposing){
				Parent = null;
				Clear ();
			}
			base.Dispose (disposing);
		}

		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = new UITableViewCell (UITableViewCellStyle.Default, "");
			cell.TextLabel.Text = "Section was used for Element";
			
			return cell;
		}
	}
	
	/// <summary>
	/// Used by root elements to fetch information when they need to
	/// render a summary (Checkbox count or selected radio group).
	/// </summary>
	[PublicAPI]
	public class Group {
		public string? Key;
		public Group (string? key)
		{
			Key = key;
		}
	}
	/// <summary>
	/// Captures the information about mutually exclusive elements in a RootElement
	/// </summary>
	public class RadioGroup : Group {
		int _selected;
		public virtual int Selected {
			get => _selected;
			set => _selected = value;
		}
		
		public RadioGroup (string? key, int selected) : base (key)
		{
			this._selected = selected;
		}
		
		public RadioGroup (int selected) : base (null)
		{
			this._selected = selected;
		}
	}
	
	/// <summary>
	///    RootElements are responsible for showing a full configuration page.
	/// </summary>
	/// <remarks>
	///    At least one RootElement is required to start the MonoTouch.Dialogs
	///    process.   
	/// 
	///    RootElements can also be used inside Sections to trigger
	///    loading a new nested configuration page.   When used in this mode
	///    the caption provided is used while rendered inside a section and
	///    is also used as the Title for the subpage.
	/// 
	///    If a RootElement is initialized with a section/element value then
	///    this value is used to locate a child Element that will provide
	///    a summary of the configuration which is rendered on the right-side
	///    of the display.
	/// 
	///    RootElements are also used to coordinate radio elements.  The
	///    RadioElement members can span multiple Sections (for example to
	///    implement something similar to the ring tone selector and separate
	///    custom ring tones from system ringtones).
	/// 
	///    Sections are added by calling the Add method which supports the
	///    C# 4.0 syntax to initialize a RootElement in one pass.
	/// </remarks>
	[PublicAPI]
	public class RootElement : Element, IEnumerable<Section> {
		static readonly NSString RKey1 = new("RootElement1");
		static readonly NSString RKey2 = new("RootElement2");
		readonly int _summarySection;
		readonly int _summaryElement;
		internal readonly Group? Group;
		public bool UnevenRows;
		public Func<RootElement, UIViewController>? CreateOnSelected;
		public UITableView? TableView;
		
		// This is used to indicate that we need the DVC to dispatch calls to
		// WillDisplayCell so we can prepare the color of the cell before 
		// display
		public bool NeedColorUpdate;
		
		/// <summary>
		///  Initializes a RootSection with a caption
		/// </summary>
		/// <param name="caption">
		///  The caption to render.
		/// </param>
		public RootElement (string caption) : base (caption)
		{
			_summarySection = -1;
			Sections = new List<Section> ();
		}

		/// <summary>
		/// Initializes a RootSection with a caption and a callback that will
		/// create the nested UIViewController that is activated when the user
		/// taps on the element.
		/// </summary>
		/// <param name="caption">
		///  The caption to render.
		/// </param>
		/// <param name="createOnSelected"></param>
		public RootElement (string caption, Func<RootElement, UIViewController> createOnSelected) : base (caption)
		{
			_summarySection = -1;
			this.CreateOnSelected = createOnSelected;
			Sections = new List<Section> ();
		}
		
		/// <summary>
		///   Initializes a RootElement with a caption with a summary fetched from the specified section and element
		/// </summary>
		/// <param name="caption">
		/// The caption to render cref="System.String"/>
		/// </param>
		/// <param name="section">
		/// The section that contains the element with the summary.
		/// </param>
		/// <param name="element">
		/// The element index inside the section that contains the summary for this RootSection.
		/// </param>
		public 	RootElement (string caption, int section, int element) : base (caption)
		{
			_summarySection = section;
			_summaryElement = element;
		}
		
		/// <summary>
		/// Initializes a RootElement that renders the summary based on the radio settings of the contained elements. 
		/// </summary>
		/// <param name="caption">
		/// The caption to ender
		/// </param>
		/// <param name="group">
		/// The group that contains the checkbox or radio information.  This is used to display
		/// the summary information when a RootElement is rendered inside a section.
		/// </param>
		public RootElement (string caption, Group group) : base (caption)
		{
			this.Group = group;
		}
		
		internal List<Section> Sections = new List<Section> ();

		internal NSIndexPath? PathForRadio (int idx)
		{
			RadioGroup? radio = Group as RadioGroup;
			if (radio == null)
				return null;
			
			uint current = 0, section = 0;
			foreach (Section s in Sections){
				uint row = 0;
				
				foreach (Element e in s.Elements){
					if (!(e is RadioElement))
						continue;
					
					if (current == idx){
						return NSIndexPath.Create(section, row); 
					}
					row++;
					current++;
				}
				section++;
			}
			return null;
		}
		
		public int Count => Sections.Count;

		public Section this [int idx] => Sections [idx];

		internal int IndexOf (Section target)
		{
			int idx = 0;
			foreach (Section s in Sections){
				if (s == target)
					return idx;
				idx++;
			}
			return -1;
		}
			
		public void Prepare ()
		{
			int current = 0;
			foreach (Section s in Sections){				
				foreach (var e in s.Elements){
					if (e is RadioElement re)
						re.RadioIdx = current++;
					if (UnevenRows == false && e is IElementSizing)
						UnevenRows = true;
					if (NeedColorUpdate == false && e is IColorizeBackground)
						NeedColorUpdate = true;
				}
			}
		}
		
		/// <summary>
		/// Adds a new section to this RootElement
		/// </summary>
		/// <param name="section">
		/// The section to add, if the root is visible, the section is inserted with no animation
		/// </param>
		public void Add (Section? section)
		{
			if (section == null)
				return;
			
			Sections.Add (section);
			section.Parent = this;
			if (TableView == null)
				return;
			
			TableView.InsertSections (MakeIndexSet(Sections.Count-1, 1), UITableViewRowAnimation.None);
		}

		//
		// This makes things LINQ friendly;  You can now create RootElements
		// with an embedded LINQ expression, like this:
		// new RootElement ("Title") {
		//     from x in names
		//         select new Section (x) { new StringElement ("Sample") }
		//
		public void Add (IEnumerable<Section> sections)
		{
			foreach (var s in sections)
				Add (s);
		}

		static NSIndexSet MakeIndexSet (int start, int count)
		{
			NSRange range;
			range.Location = start;
			range.Length = count;
			return NSIndexSet.FromNSRange (range);
		}
		
		/// <summary>
		/// Inserts a new section into the RootElement
		/// </summary>
		/// <param name="idx">
		/// The index where the section is added <see cref="System.Int32"/>
		/// </param>
		/// <param name="anim">
		/// The <see cref="UITableViewRowAnimation"/> type.
		/// </param>
		/// <param name="newSections">
		/// A <see cref="Section[]"/> list of sections to insert
		/// </param>
		/// <remarks>
		///    This inserts the specified list of sections (a params argument) into the
		///    root using the specified animation.
		/// </remarks>
		public void Insert (int idx, UITableViewRowAnimation anim, params Section []? newSections)
		{
			if (idx < 0 || idx > Sections.Count)
				return;
			if (newSections == null)
				return;
			
			if (TableView != null)
				TableView.BeginUpdates ();
			
			int pos = idx;
			foreach (var s in newSections){
				s.Parent = this;
				Sections.Insert (pos++, s);
			}
			
			if (TableView == null)
				return;
			
			TableView.InsertSections (MakeIndexSet(idx, newSections.Length), anim);
			TableView.EndUpdates ();
		}

		/// <summary>
		/// Inserts a new section into the RootElement
		/// </summary>
		/// <param name="idx">
		/// The index where the section is added <see cref="System.Int32"/>
		/// </param>
		/// <param name="section">
		/// A <see cref="Section"/> section to insert
		/// </param>
		/// <remarks>
		///    This inserts the specified list of sections (a params argument) into the
		///    root using the Fade animation.
		/// </remarks>
		public void Insert (int idx, Section section)
		{
			Insert (idx, UITableViewRowAnimation.None, section);
		}
		
		/// <summary>
		/// Removes a section at a specified location
		/// </summary>
		public void RemoveAt (int idx)
		{
			RemoveAt (idx, UITableViewRowAnimation.Fade);
		}

		/// <summary>
		/// Removes a section at a specified location using the specified animation
		/// </summary>
		/// <param name="idx">
		/// A <see cref="System.Int32"/>
		/// </param>
		/// <param name="anim">
		/// A <see cref="UITableViewRowAnimation"/>
		/// </param>
		public void RemoveAt (int idx, UITableViewRowAnimation anim)
		{
			if (idx < 0 || idx >= Sections.Count)
				return;
			
			Sections.RemoveAt (idx);
			
			TableView?.DeleteSections (NSIndexSet.FromIndex (idx), anim);
		}
			
		public void Remove (Section? s)
		{
			if (s == null)
				return;
			int idx = Sections.IndexOf (s);
			if (idx == -1)
				return;
			RemoveAt (idx, UITableViewRowAnimation.Fade);
		}
		
		public void Remove (Section? s, UITableViewRowAnimation anim)
		{
			if (s == null)
				return;
			int idx = Sections.IndexOf (s);
			if (idx == -1)
				return;
			RemoveAt (idx, anim);
		}

		public void Clear ()
		{
			foreach (var s in Sections)
				s.Dispose ();
			Sections = new List<Section> ();
			TableView?.ReloadData ();
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing){
				TableView = null;
				Clear ();
			}
		}
		
		/// <summary>
		/// Enumerator that returns all the sections in the RootElement.
		/// </summary>
		/// <returns>
		/// A <see cref="IEnumerator"/>
		/// </returns>
		IEnumerator IEnumerable.GetEnumerator ()
		{
			foreach (var s in Sections)
				yield return s;
		}
		
		IEnumerator<Section> IEnumerable<Section>.GetEnumerator ()
		{
			foreach (var s in Sections)
				yield return s;
		}

		/// <summary>
		/// The currently selected Radio item in the whole Root.
		/// </summary>
		public int RadioSelected {
			get {
				if (Group is RadioGroup radio)
					return radio.Selected;
				return -1;
			}
			set {
				if (Group is RadioGroup radio)
					radio.Selected = value;
			}
		}
		
		public override UITableViewCell GetCell (UITableView tv)
		{
			NSString key = _summarySection == -1 ? RKey1 : RKey2;
			var cell = tv.DequeueReusableCell (key);
			if (cell == null){
				var style = _summarySection == -1 ? UITableViewCellStyle.Default : UITableViewCellStyle.Value1;
				
				cell = new UITableViewCell (style, key);
				cell.SelectionStyle = UITableViewCellSelectionStyle.Blue;
			} 
		
			cell.TextLabel.Text = Caption;
			if (Group is RadioGroup radio){
				var selected = radio.Selected;
				var current = 0;
				
				foreach (var s in Sections){
					foreach (var e in s.Elements){
						if (!(e is RadioElement))
							continue;
						
						if (current == selected){
							cell.DetailTextLabel.Text = e.Summary ();
							goto le;
						}
						current++;
					}
				}
			} else if (Group != null){
				int count = 0;
				
				foreach (var s in Sections){
					foreach (var e in s.Elements){
						if (e is CheckboxElement ce){
							if (ce.Value)
								count++;
							continue;
						}

						if (e is BoolElement { Value: true }) count++;
					}
				}
				cell.DetailTextLabel.Text = count.ToString ();
			} else if (_summarySection != -1 && _summarySection < Sections.Count){
					var s = Sections [_summarySection];
					if (_summaryElement < s.Elements.Count && cell.DetailTextLabel != null)
						cell.DetailTextLabel.Text = s.Elements [_summaryElement].Summary ();
			} 
			le:
			cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
			
			return cell;
		}
		
		/// <summary>
		///    This method does nothing by default, but gives a chance to subclasses to
		///    customize the UIViewController before it is presented
		/// </summary>
		protected virtual void PrepareDialogViewController (UIViewController dvc)
		{
		}
		
		/// <summary>
		/// Creates the UIViewController that will be pushed by this RootElement
		/// </summary>
		protected virtual UIViewController MakeViewController ()
		{
			if (CreateOnSelected != null)
				return CreateOnSelected (this);
			
			return new DialogViewController (this, true) {
				Autorotate = true
			};
		}
		
		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
		{
			tableView.DeselectRow (path, false);
			var newDvc = MakeViewController ();
			PrepareDialogViewController (newDvc);
			dvc.ActivateController (newDvc);
		}
		
		public void Reload (Section section, UITableViewRowAnimation animation)
		{
			if (section == null)
				throw new ArgumentNullException (nameof(section));
			if (section.Parent == null || section.Parent != this)
				throw new ArgumentException ("Section is not attached to this root");
			
			int idx = 0;
			foreach (var sect in Sections){
				if (sect == section){
					TableView?.ReloadSections (new NSIndexSet ((uint) idx), animation);
					return;
				}
				idx++;
			}
		}
		
		public void Reload (Element element, UITableViewRowAnimation animation)
		{
			if (element == null)
				throw new ArgumentNullException (nameof(element));
			var section = element.Parent as Section;
			if (section == null)
				throw new ArgumentException ("Element is not attached to this root");
			var root = section.Parent as RootElement;
			if (root == null)
				throw new ArgumentException ("Element is not attached to this root");
			var path = element.IndexPath;
			if (path == null)
				return;
			TableView?.ReloadRows (new [] { path }, animation);
		}
	}
}
