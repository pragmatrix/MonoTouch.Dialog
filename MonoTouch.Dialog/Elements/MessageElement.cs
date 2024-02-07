using System;

using UIKit;
using Foundation;
using CoreGraphics;
using JetBrains.Annotations;

namespace MonoTouch.Dialog {

	public class MessageSummaryView : UIView {
		static readonly UIFont SenderFont = UIFont.BoldSystemFontOfSize (19);
		static readonly UIFont SubjectFont = UIFont.SystemFontOfSize (14);
		static readonly UIFont TextFont = UIFont.SystemFontOfSize (13);
		static readonly UIFont CountFont = UIFont.BoldSystemFontOfSize (13);
		public string? Sender { get; private set; }
		public string? Body { get; private set; }
		public string? Subject { get; private set; }
		public DateTime Date { get; private set; }
		public bool NewFlag  { get; private set; }
		public int MessageCount  { get; private set; }
		
		static readonly CGGradient Gradient;
		
		static MessageSummaryView ()
		{
			using var colorspace = CGColorSpace.CreateDeviceRGB ();
			Gradient = new CGGradient (colorspace, new nfloat [] { /* first */ .52f, .69f, .96f, 1, /* second */ .12f, .31f, .67f, 1 }, null); //new float [] { 0, 1 });
		}
		
		public MessageSummaryView ()
		{
			base.BackgroundColor = UIColor.White;
		}
		
		public void Update (string? sender, string? body, string? subject, DateTime date, bool newFlag, int messageCount)
		{
			Sender = sender;
			Body = body;
			Subject = subject;
			Date = date;
			NewFlag = newFlag;
			MessageCount = messageCount;
			SetNeedsDisplay ();
		}
		
		public override void Draw (CGRect rect)
		{
			const int PadRight = 21;
			var ctx = UIGraphics.GetCurrentContext ();
			nfloat boxWidth;
			CGSize sSize;
			
			if (MessageCount > 0){
				var ms = MessageCount.ToString ();
				sSize = ms.StringSize (CountFont);
				boxWidth = (nfloat)Math.Min (22 + sSize.Width, 18);
				var cRect = new CGRect (Bounds.Width-20-boxWidth, 32, boxWidth, 16);
				
				UIColor.Gray.SetFill ();
				GraphicsUtil.FillRoundedRect (ctx, cRect, 3);
				UIColor.White.SetColor ();
				cRect.X += 5;
				ms.DrawString (cRect, CountFont);
				
				boxWidth += PadRight;
			} else
				boxWidth = 0;
			
			UIColor.FromRGB (36, 112, 216).SetColor ();
			var diff = DateTime.Now - Date;
			var now = DateTime.Now;
			string label;
			if (now.Day == Date.Day && now.Month == Date.Month && now.Year == Date.Year)
				label = Date.ToShortTimeString ();
			else if (diff <= TimeSpan.FromHours (24))
				label = "Yesterday".GetText ();
			else if (diff < TimeSpan.FromDays (6))
				label = Date.ToString ("dddd");
			else
				label = Date.ToShortDateString ();
			sSize = label.StringSize (SubjectFont);
			nfloat dateSize = sSize.Width + PadRight + 5;
			label.DrawString (new CGRect (Bounds.Width-dateSize, 6, dateSize, 14), SubjectFont, UILineBreakMode.Clip, UITextAlignment.Left);
			
			const int Offset = 33;
			nfloat bw = Bounds.Width-Offset;
			
			UIColor.Black.SetColor ();
			Sender.DrawString (new CGPoint (Offset, 2), (float)(bw-dateSize), SenderFont, UILineBreakMode.TailTruncation);
			Subject.DrawString (new CGPoint (Offset, 23), (float)(bw-Offset-boxWidth), SubjectFont, UILineBreakMode.TailTruncation);
			
			//UIColor.Black.SetFill ();
			//ctx.FillRect (new CGRect (offset, 40, bw-boxWidth, 34));
			UIColor.Gray.SetColor ();
			Body.DrawString (new CGRect (Offset, 40, bw-boxWidth, 34), TextFont, UILineBreakMode.TailTruncation, UITextAlignment.Left);
			
			if (NewFlag){
				ctx.SaveState ();
				ctx.AddEllipseInRect (new CGRect (10, 32, 12, 12));
				ctx.Clip ();
				ctx.DrawLinearGradient (Gradient, new CGPoint (10, 32), new CGPoint (22, 44), CGGradientDrawingOptions.DrawsAfterEndLocation);
				ctx.RestoreState ();
			}
			
#if WANT_SHADOWS
			ctx.SaveState ();
			UIColor.FromRGB (78, 122, 198).SetStroke ();
			ctx.SetShadow (new CGSize (1, 1), 3);
			ctx.StrokeEllipseInRect (new CGRect (10, 32, 12, 12));
			ctx.RestoreState ();
#endif
		}
	}
		
	[PublicAPI]
	public class MessageElement : Element, IElementSizing {
		static readonly NSString MKey = new("MessageElement");

		public string? Sender;
		public string? Body;
		public string? Subject;
		public DateTime Date;
		public bool NewFlag;
		public int MessageCount;
		
		class MessageCell : UITableViewCell {
			readonly MessageSummaryView _view;
			
			public MessageCell () : base (UITableViewCellStyle.Default, MKey)
			{
				_view = new MessageSummaryView ();
				base.ContentView.Add (_view);
				base.Accessory = UITableViewCellAccessory.DisclosureIndicator;
			}
			
			public void Update (MessageElement me)
			{
				_view.Update (me.Sender, me.Body, me.Subject, me.Date, me.NewFlag, me.MessageCount);
			}
			
			public override void LayoutSubviews ()
			{
				base.LayoutSubviews ();
				_view.Frame = ContentView.Bounds;
				_view.SetNeedsDisplay ();
			}
		}
		
		public MessageElement () : base ("")
		{
		}
		
		public MessageElement (Action<DialogViewController,UITableView,NSIndexPath> tapped) : base ("")
		{
			Tapped += tapped;
		}
		
		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = tv.DequeueReusableCell (MKey) as MessageCell ?? new MessageCell ();
			cell.Update (this);
			return cell;
		}
		
		public nfloat GetHeight (UITableView tableView, NSIndexPath indexPath)
		{
			return 78;
		}
		
		public event Action<DialogViewController, UITableView, NSIndexPath>? Tapped;
		
		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
		{
			Tapped?.Invoke (dvc, tableView, path);
		}

		public override bool Matches (string text)
		{
			if (Sender != null && Sender.IndexOf (text, StringComparison.CurrentCultureIgnoreCase) != -1)
				return true;
			if (Body != null && Body.IndexOf (text, StringComparison.CurrentCultureIgnoreCase) != -1)
				return true;
			if (Subject != null && Subject.IndexOf (text, StringComparison.CurrentCultureIgnoreCase) != -1)
				return true;

			return false;
		}
	}
}

