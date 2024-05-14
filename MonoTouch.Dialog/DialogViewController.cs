//
// DialogViewController.cs: drives MonoTouch.Dialog
//
// Author:
//   Miguel de Icaza
//
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Foundation;
using UIKit;
using CoreGraphics;
using JetBrains.Annotations;

namespace MonoTouch.Dialog
{
	/// <summary>
	///   The DialogViewController is the main entry point to use MonoTouch.Dialog,
	///   it provides a simplified API to the UITableViewController.
	/// </summary>
	[PublicAPI]
	public class DialogViewController : UITableViewController
	{
		public UITableViewStyle Style = UITableViewStyle.Grouped;
		public event Action<NSIndexPath>? OnSelection;
#if !__TVOS__
		UISearchBar? _searchBar;
#endif
		UITableView? _tableView;
		RootElement? _root;
		bool _pushing;
		bool _dirty;
		bool _reloading;

		/// <summary>
		/// The root element displayed by the DialogViewController, the value can be changed during runtime to update the contents.
		/// </summary>
		public RootElement? Root {
			get => _root;
			set {
				if (_root == value)
					return;
				_root?.Dispose ();

				_root = value;
				if (_root != null)
					_root.TableView = _tableView;		
				ReloadData ();
			}
		} 

		EventHandler? _refreshRequested;
		/// <summary>
		/// If you assign a handler to this event before the view is shown, the
		/// DialogViewController will have support for pull-to-refresh UI.
		/// </summary>
		public event EventHandler RefreshRequested {
			add {
				if (_tableView != null)
					throw new ArgumentException ("You should set the handler before the controller is shown");
				_refreshRequested += value; 
			}
			remove => _refreshRequested -= value;
		}
		
		// If the value is true, we are enabled, used in the source for quick computation
		bool _enableSearch;
		public bool EnableSearch {
			get => _enableSearch;
			set {
				if (_enableSearch == value)
					return;
				
				// After MonoTouch 3.0, we can allow for the search to be enabled/disable
				if (_tableView != null)
					throw new ArgumentException ("You should set EnableSearch before the controller is shown");
				_enableSearch = value;
			}
		}
		
		// If set, we automatically scroll the content to avoid showing the search bar until 
		// the user manually pulls it down.
		public bool AutoHideSearch { get; set; }
		
		public string? SearchPlaceholder { get; set; }
			
		/// <summary>
		/// Invoke this method to trigger a data refresh.   
		/// </summary>
		/// <remarks>
		/// This will invoke the RefreshRequested event handler, the code attached to it
		/// should start the background operation to fetch the data and when it completes
		/// it should call ReloadComplete to restore the control state.
		/// </remarks>
		public void TriggerRefresh ()
		{
			TriggerRefresh (false);
		}
		
		void TriggerRefresh (bool showStatus)
		{
			_ = showStatus;
			
			if (_refreshRequested == null)
				return;

			if (_reloading)
				return;
			
			_reloading = true;
			_refreshRequested (this, EventArgs.Empty);
		}
		
		/// <summary>
		/// Invoke this method to signal that a reload has completed, this will update the UI accordingly.
		/// </summary>
		public void ReloadComplete ()
		{
			if (!_reloading)
				return;

			_reloading = false;

#if !__TVOS__
			RefreshControl?.EndRefreshing ();
#endif
		}
		
		/// <summary>
		/// Controls whether the DialogViewController should auto rotate
		/// </summary>
		public bool Autorotate { get; set; }

#if !__TVOS__
		public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
		{
			return Autorotate || toInterfaceOrientation == UIInterfaceOrientation.Portrait;
		}
		
		public override void DidRotate (UIInterfaceOrientation fromInterfaceOrientation)
		{
			base.DidRotate (fromInterfaceOrientation);
			
			//Fixes the RefreshView's size if it is shown during rotation
			ReloadData ();
		}
#endif
		
		Section []? _originalSections;
		Element [][]? _originalElements;
		
		/// <summary>
		/// Allows caller to programatically activate the search bar and start the search process
		/// </summary>
		public void StartSearch ()
		{
			if (Root is null || _originalSections is not null)
				return;
			
#if !__TVOS__
			_searchBar?.BecomeFirstResponder ();
#endif
			_originalSections = Root.Sections.ToArray ();
			_originalElements = new Element [_originalSections.Length][];
			for (int i = 0; i < _originalSections.Length; i++)
				_originalElements [i] = _originalSections [i].Elements.ToArray ();
		}
		
		/// <summary>
		/// Allows the caller to programatically stop searching.
		/// </summary>
		public virtual void FinishSearch ()
		{
			if (Root is null || _originalSections is null)
				return;
			
			Root.Sections = [.._originalSections];
			_originalSections = null;
			_originalElements = null;
#if !__TVOS__
			_searchBar?.ResignFirstResponder ();
#endif
			ReloadData ();
		}
		
		public delegate void SearchTextEventHandler (object sender, SearchChangedEventArgs args);
		public event SearchTextEventHandler? SearchTextChanged;
		
		public virtual void OnSearchTextChanged (string text)
		{
			SearchTextChanged?.Invoke (this, new SearchChangedEventArgs (text));
		}
		                                     
		public void PerformFilter (string text)
		{
			if (_originalSections is null || _originalElements is null || Root is null)
				return;
			
			OnSearchTextChanged (text);
			
			var newSections = new List<Section> ();
			
			for (var sIdX = 0; sIdX < _originalSections.Length; sIdX++){
				Section? newSection = null;
				var section = _originalSections [sIdX];
				var elements = _originalElements [sIdX];
				
				foreach (var t in elements)
				{
					if (!t.Matches(text)) 
						continue;
					if (newSection == null){
						newSection = new Section (section.Header, section.Footer){
							FooterView = section.FooterView,
							HeaderView = section.HeaderView
						};
						newSections.Add (newSection);
					}
					newSection.Add (t);
				}
			}
			
			Root.Sections = newSections;
			
			ReloadData ();
		}
		
		public virtual void SearchButtonClicked (string text)
		{
		}
			
		class SearchDelegate : UISearchBarDelegate {
			readonly DialogViewController _container;
			
			public SearchDelegate (DialogViewController container)
			{
				_container = container;
			}
			
			public override void OnEditingStarted (UISearchBar searchBar)
			{
#if !__TVOS__
				searchBar.ShowsCancelButton = true;
#endif
				_container.StartSearch ();
			}
			
			public override void OnEditingStopped (UISearchBar searchBar)
			{
#if !__TVOS__
				searchBar.ShowsCancelButton = false;
#endif
				_container.FinishSearch ();
			}
			
			public override void TextChanged (UISearchBar searchBar, string searchText)
			{
				_container.PerformFilter (searchText);
			}
			
#if !__TVOS__
			public override void CancelButtonClicked (UISearchBar searchBar)
			{
				Trace.Assert(_container._searchBar is not null);

				searchBar.ShowsCancelButton = false;
				_container._searchBar.Text = "";
				_container.FinishSearch ();
				searchBar.ResignFirstResponder ();
			}
#endif
			
			public override void SearchButtonClicked (UISearchBar searchBar)
			{
				_container.SearchButtonClicked (searchBar.Text ?? "");
			}
		}
		
		[PublicAPI]
		public class Source : UITableViewSource {
			const float YBoundary = 65;
			readonly WeakReference<DialogViewController> _container;
			protected DialogViewController? Container => _container.TryGetTarget (out var result) ? result : null;
			protected RootElement Root;
			
			public Source (DialogViewController container)
			{
				Trace.Assert(container._root is not null);
				_container = new WeakReference<DialogViewController> (container);
				Root = container._root;
			}
			
			public override void AccessoryButtonTapped (UITableView tableView, NSIndexPath indexPath)
			{
				var section = Root.Sections [indexPath.Section];
				if (section.Elements [indexPath.Row] is StyledStringElement element)
					element.AccessoryTap ();
			}
			
			public override nint RowsInSection (UITableView tableview, nint section)
			{
				var s = Root.Sections [(int) section];
				var count = s.Elements.Count;
				
				return count;
			}

			public override nint NumberOfSections (UITableView tableView)
			{
				return Root.Sections.Count;
			}

			public override string TitleForHeader (UITableView tableView, nint section)
			{
				return Root.Sections [(int) section].Caption!;
			}

			public override string TitleForFooter (UITableView tableView, nint section)
			{
				return Root.Sections [(int) section].Footer!;
			}

			public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
			{
				var section = Root.Sections [indexPath.Section];
				var element = section.Elements [indexPath.Row];
				
				return element.GetCell (tableView);
			}
			
			public override void WillDisplay (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
			{
				if (Root.NeedColorUpdate){
					var section = Root.Sections [indexPath.Section];
					var element = section.Elements [indexPath.Row];
					var colorized = element as IColorizeBackground;
					colorized?.WillDisplay (tableView, cell, indexPath);
				}
			}
			
			public override void RowDeselected (UITableView tableView, NSIndexPath indexPath)
			{
				Container?.Deselected (indexPath);
			}
			
			public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
			{
				if (Container is null) 
					return;
				var onSelection = Container.OnSelection;
				if (onSelection != null)
					onSelection (indexPath);
				Container.Selected (indexPath);
			}			
			
			public override UIView GetViewForHeader (UITableView tableView, nint sectionIdx)
			{
				var section = Root.Sections [(int) sectionIdx];
				return section.HeaderView!;
			}

			public override nfloat GetHeightForHeader (UITableView tableView, nint sectionIdx)
			{
				var section = Root.Sections [(int) sectionIdx];
				if (section.HeaderView == null)
					return -1;
				return section.HeaderView.Frame.Height;
			}

			public override UIView GetViewForFooter (UITableView tableView, nint sectionIdx)
			{
				var section = Root.Sections [(int) sectionIdx];
				return section.FooterView!;
			}
			
			public override nfloat GetHeightForFooter (UITableView tableView, nint sectionIdx)
			{
				var section = Root.Sections [(int) sectionIdx];
				if (section.FooterView == null)
					return -1;
				return section.FooterView.Frame.Height;
			}			

			public override void Scrolled (UIScrollView scrollView) 
			{
				
			}

			public override void DraggingStarted (UIScrollView scrollView)
			{
				
			}

			public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
			{
			}
		}

		//
		// Performance trick, if we expose GetHeightForRow, the UITableView will
		// probe *every* row for its size;   Avoid this by creating a separate
		// model that is used only when we have items that require resizing
		//
		public class SizingSource : Source {
			public SizingSource (DialogViewController controller) : base (controller) {}
			
			public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
			{
				var section = Root.Sections [indexPath.Section];
				var element = section.Elements [indexPath.Row];
				
				var sizable = element as IElementSizing;
				if (sizable == null)
					return tableView.RowHeight;
				return sizable.GetHeight (tableView, indexPath);
			}
		}
			
		/// <summary>
		/// Activates a nested view controller from the DialogViewController.   
		/// If the view controller is hosted in a UINavigationController it
		/// will push the result.   Otherwise it will show it as a modal
		/// dialog
		/// </summary>
		public void ActivateController (UIViewController controller)
		{
			_dirty = true;
			
			var parent = ParentViewController;

			// We can not push a nav controller into a nav controller
			if (parent is UINavigationController nav && !(controller is UINavigationController))
				nav.PushViewController (controller, true);
			else {
#if __TVOS__
				PresentViewController (controller, true, null);
#else
				PresentModalViewController (controller, true);
#endif
			}
		}

		/// <summary>
		/// Dismisses the view controller.   It either pops or dismisses
		/// based on the kind of container we are hosted in.
		/// </summary>
		[PublicAPI]
		public void DeactivateController (bool animated)
		{
			var parent = ParentViewController;
			if (parent is UINavigationController nav)
				nav.PopViewController (animated);
			else {
#if __TVOS__
				DismissViewController (animated, null);
#else
				DismissModalViewController (animated);
#endif
			}
		}

		void SetupSearch ()
		{
#if __TVOS__
			// Can't create a UISearchBar in tvOS, you can only use one from a UISearchController,
			// which require bigger changes, so just skip this for now.
#else
			if (_enableSearch){
				Trace.Assert(_tableView is not null);
				_searchBar = new UISearchBar (new CGRect (0, 0, _tableView.Bounds.Width, 44)) {
					Delegate = new SearchDelegate (this)
				};
				if (SearchPlaceholder != null)
					_searchBar.Placeholder = this.SearchPlaceholder;
				_tableView.TableHeaderView = _searchBar;					
			} else {
				// Does not work with current Monotouch, will work with 3.0
				// tableView.TableHeaderView = null;
			}
#endif
		}
		
		public virtual void Deselected (NSIndexPath indexPath)
		{
			Trace.Assert(_root is not null && _tableView is not null);
			
			var section = _root.Sections [indexPath.Section];
			var element = section.Elements [indexPath.Row];
			
			element.Deselected (this, _tableView, indexPath);
		}
		
		public virtual void Selected (NSIndexPath indexPath)
		{
			Trace.Assert(_root is not null && _tableView is not null);
			
			var section = _root.Sections [indexPath.Section];
			var element = section.Elements [indexPath.Row];

			element.Selected (this, _tableView, indexPath);
		}
		
		public virtual UITableView MakeTableView (CGRect bounds, UITableViewStyle style)
		{
			return new UITableView (bounds, style);
		}
		
		public override void LoadView ()
		{
			_tableView = MakeTableView (UIScreen.MainScreen.Bounds, Style);
			_tableView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin;
			_tableView.AutosizesSubviews = true;
			
			if (_root != null)
				_root.Prepare ();
			
			UpdateSource ();
			View = _tableView;
			SetupSearch ();
			ConfigureTableView ();
			
			if (_root == null)
				return;
			_root.TableView = _tableView;
		}

		void ConfigureTableView ()
		{
#if !__TVOS__
			if (_refreshRequested != null) {
				RefreshControl = new UIRefreshControl ();
				RefreshControl.AddTarget ((sender,args)=> TriggerRefresh (), UIControlEvent.ValueChanged);
			}
#endif
		}
		
		public event EventHandler? ViewAppearing;

		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
			if (AutoHideSearch){
				if (_enableSearch){
					if (TableView.ContentOffset.Y < 44)
						TableView.ContentOffset = new CGPoint (0, 44);
				}
			}
			if (_root == null)
				return;
			
			_root.Prepare ();

#if !__TVOS__
			NavigationItem.HidesBackButton = !_pushing;
#endif
			if (_root.Caption != null)
				NavigationItem.Title = _root.Caption;
			if (_dirty){
				_tableView?.ReloadData ();
				_dirty = false;
			}

			ViewAppearing?.Invoke (this, EventArgs.Empty);
		}

		public bool Pushing {
			get {
				return _pushing;
			}
			set {
				_pushing = value;
#if !__TVOS__
				if (NavigationItem != null)
					NavigationItem.HidesBackButton = !_pushing;
#endif
			}
		}
		
		public virtual Source CreateSizingSource (bool unevenRows)
		{
			return unevenRows ? new SizingSource (this) : new Source (this);
		}
		
		Source? _tableSource;
		
		void UpdateSource ()
		{
			if (_root == null || _tableView == null)
				return;
			
			_tableSource = CreateSizingSource (_root.UnevenRows);
			_tableView.Source = _tableSource;
		}

		public void ReloadData ()
		{
			if (_root == null)
				return;
			
			if(_root.Caption != null) 
				NavigationItem.Title = _root.Caption;
			
			_root.Prepare ();
			if (_tableView != null){
				UpdateSource ();
				_tableView.ReloadData ();
			}
			_dirty = false;
		}
		
		public event EventHandler? ViewDisappearing;
		
		public override void ViewWillDisappear (bool animated)
		{
			base.ViewWillDisappear (animated);
			ViewDisappearing?.Invoke (this, EventArgs.Empty);
		}
		
		public DialogViewController (RootElement root) : base (UITableViewStyle.Grouped)
		{
			_root = root;
		}
		
		public DialogViewController (UITableViewStyle style, RootElement root) : base (style)
		{
			Style = style;
			this._root = root;
		}
		
		/// <summary>
		///     Creates a new DialogViewController from a RootElement and sets the push status
		/// </summary>
		/// <param name="root">
		/// The <see cref="RootElement"/> containing the information to render.
		/// </param>
		/// <param name="pushing">
		/// A <see cref="System.Boolean"/> describing whether this is being pushed 
		/// (NavigationControllers) or not.   If pushing is true, then the back button 
		/// will be shown, allowing the user to go back to the previous controller
		/// </param>
		public DialogViewController (RootElement root, bool pushing) : base (UITableViewStyle.Grouped)
		{
			this._pushing = pushing;
			this._root = root;
		}

		public DialogViewController (UITableViewStyle style, RootElement root, bool pushing) : base (style)
		{
			Style = style;
			this._pushing = pushing;
			this._root = root;
		}
		public DialogViewController (IntPtr handle) : base(handle)
		{
			this._root = new RootElement ("");
		}
	}
}
