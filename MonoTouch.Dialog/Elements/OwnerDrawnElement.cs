using System.Diagnostics.CodeAnalysis;
using UIKit;
using CoreGraphics;
using Foundation;
using JetBrains.Annotations;

namespace MonoTouch.Dialog
{
	[PublicAPI]
	public abstract class OwnerDrawnElement : Element, IElementSizing
	{		
		public string CellReuseIdentifier
		{
			get;set;	
		}
		
		public UITableViewCellStyle Style
		{
			get;set;	
		}

		protected OwnerDrawnElement (UITableViewCellStyle style, string cellIdentifier) : base(null)
		{
			CellReuseIdentifier = cellIdentifier;
			Style = style;
		}
		
		public nfloat GetHeight (UITableView tableView, NSIndexPath indexPath)
		{
			return Height(tableView.Bounds);
		}
		
		public override UITableViewCell GetCell (UITableView tv)
		{
			OwnerDrawnCell? cell = tv.DequeueReusableCell(this.CellReuseIdentifier) as OwnerDrawnCell;
			
			if (cell == null)
			{
				cell = new OwnerDrawnCell(this, this.Style, this.CellReuseIdentifier);
			}
			else
			{
				cell.Element = this;
			}
			
			cell.Update();
			return cell;
		}	
		
		public abstract void Draw(CGRect bounds, CGContext context, UIView view);
		
		public abstract nfloat Height(CGRect bounds);
		
		class OwnerDrawnCell : UITableViewCell
		{
			OwnerDrawnCellView? _view;
			
			public OwnerDrawnCell(OwnerDrawnElement element, UITableViewCellStyle style, string cellReuseIdentifier) : base(style, cellReuseIdentifier)
			{
				Element = element;
			}
			
			[DisallowNull]
			public OwnerDrawnElement? Element
			{
				get => _view?.Element;
				set {
					if (_view == null)
					{
						_view = new OwnerDrawnCellView (value);
						ContentView.Add (_view);
					}
					else
					{
						_view.Element = value;
					}
				}
			}
				
			

			public void Update()
			{
				SetNeedsDisplay();
				_view?.SetNeedsDisplay();
			}		
	
			public override void LayoutSubviews()
			{
				base.LayoutSubviews();

				if (_view is not null) 
					_view.Frame = ContentView.Bounds;
			}
		}
		
		class OwnerDrawnCellView : UIView
		{				
			OwnerDrawnElement _element;
			
			public OwnerDrawnCellView(OwnerDrawnElement element)
			{
				_element = element;
			}
			
			
			public OwnerDrawnElement Element
			{
				get => _element;
				set => _element = value;
			}
			
			public void Update()
			{
				SetNeedsDisplay();
			
			}
			
			public override void Draw (CGRect rect)
			{
				CGContext context = UIGraphics.GetCurrentContext();
				_element.Draw(rect, context, this);
			}
		}
	}
}

