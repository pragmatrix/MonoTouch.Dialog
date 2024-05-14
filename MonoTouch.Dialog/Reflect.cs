//
// Reflect.cs: Creates Element classes from an instance
//
// Author:
//   Miguel de Icaza (miguel@gnome.org)
//
// Copyright 2010, Novell, Inc.
//
// Code licensed under the MIT X11 license
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using UIKit;

using NSAction = System.Action;

namespace MonoTouch.Dialog
{
	[PublicAPI]
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited=false)]
	public class EntryAttribute : Attribute {
		public EntryAttribute () : this (null) { }

		public EntryAttribute (string? placeholder)
		{
			Placeholder = placeholder;
		}

		public string? Placeholder;
		public UIKeyboardType KeyboardType;
		public UITextAutocorrectionType AutocorrectionType;
		public UITextAutocapitalizationType AutocapitalizationType;
		public UITextFieldViewMode ClearButtonMode;
	}

	[PublicAPI]
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited=false)]
	public class DateAttribute : Attribute { }
	
	[PublicAPI]
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited=false)]
	public class TimeAttribute : Attribute { }
	
	[PublicAPI]
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited=false)]
	public class CheckboxAttribute : Attribute {}

	[PublicAPI]
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited=false)]
	public class MultilineAttribute : Attribute {}
	
	[PublicAPI]
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited=false)]
	public class HtmlAttribute : Attribute {}
	
	[PublicAPI]
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited=false)]
	public class SkipAttribute : Attribute {}
	
	[PublicAPI]
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited=false)]
	public class PasswordAttribute : EntryAttribute {
		public PasswordAttribute (string placeholder) : base (placeholder) {}
	}

	[PublicAPI]
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited=false)]
	public class AlignmentAttribute : Attribute {
		public AlignmentAttribute (UITextAlignment alignment) {
			Alignment = alignment;
		}
		public UITextAlignment Alignment;
	}
	
	[PublicAPI]
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited=false)]
	public class RadioSelectionAttribute : Attribute {
		public string Target;
		public RadioSelectionAttribute (string target) 
		{
			Target = target;
		}
	}

	[PublicAPI]
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited=false)]
	public class OnTapAttribute : Attribute {
		public OnTapAttribute (string method)
		{
			Method = method;
		}
		public string Method;
	}
	
	[PublicAPI]
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited=false)]
	public class CaptionAttribute : Attribute {
		public CaptionAttribute (string caption)
		{
			Caption = caption;
		}
		public string Caption;
	}

	[PublicAPI]
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited=false)]
	public class SectionAttribute : Attribute {
		public SectionAttribute () {}
		
		public SectionAttribute (string caption)
		{
			Caption = caption;
		}
			
		public SectionAttribute (string caption, string footer)
		{
			Caption = caption;
			Footer = footer;
		}

		public string? Caption;
		public string? Footer;
	}

	
	[PublicAPI]
	public class RangeAttribute : Attribute {
		public RangeAttribute (float low, float high)
		{
			Low = low;
			High = high;
		}
		public float Low, High;
		public bool ShowCaption;
	}

	[PublicAPI]
	public class BindingContext : IDisposable {
		public RootElement Root;
		Dictionary<Element,MemberAndInstance> _mappings;
		Dictionary<StringElement, Action> _handlerMappings;
			
		class MemberAndInstance {
			public MemberAndInstance (MemberInfo mi, object o)
			{
				Member = mi;
				Obj = o;
			}
			public readonly MemberInfo Member;
			public readonly object Obj;
		}
		
		static object? GetValue (MemberInfo mi, object? o)
		{
			var fi = mi as FieldInfo;
			if (fi != null)
				return fi.GetValue (o);
			var pi = mi as PropertyInfo;
			
			var getMethod = pi?.GetGetMethod ();
			return getMethod?.Invoke (o, Array.Empty<object>());
		}

		static void SetValue (MemberInfo mi, object o, object? val)
		{
			var fi = mi as FieldInfo;
			if (fi != null){
				fi.SetValue (o, val);
				return;
			}
			var pi = mi as PropertyInfo;
			var setMethod = pi?.GetSetMethod ();
			setMethod?.Invoke (o, new[] { val });
		}
			
		static string MakeCaption (string name)
		{
			var sb = new StringBuilder (name.Length);
			bool nextUp = true;
			
			foreach (char c in name){
				if (nextUp){
					sb.Append (Char.ToUpper (c));
					nextUp = false;
				} else {
					if (c == '_'){
						sb.Append (' ');
						continue;
					}
					if (Char.IsUpper (c))
						sb.Append (' ');
					sb.Append (c);
				}
			}
			return sb.ToString ();
		}

		// Returns the type for fields and properties and null for everything else
		static Type? GetTypeForMember (MemberInfo mi)
		{
			return mi switch
			{
				FieldInfo info => info.FieldType,
				PropertyInfo info => info.PropertyType,
				_ => null
			};
		}
		
		public BindingContext (object callbacks, object o, string title)
		{
			if (o == null)
				throw new ArgumentNullException (nameof(o));
			
			_mappings = new Dictionary<Element,MemberAndInstance> ();
			_handlerMappings = new Dictionary<StringElement, NSAction> ();
			
			Root = new RootElement (title);
			Populate (callbacks, o, Root);
		}
		
		void Populate (object callbacks, object o, RootElement root)
		{
			MemberInfo? lastRadioIndex = null;
			var members = o.GetType ().GetMembers (BindingFlags.DeclaredOnly | BindingFlags.Public |
							       BindingFlags.NonPublic | BindingFlags.Instance);

			Section? section = null;
			
			foreach (var mi in members){
				Type? mType = GetTypeForMember (mi);

				if (mType == null)
					continue;

				string? caption = null;
				object [] attrs = mi.GetCustomAttributes (false);
				bool skip = false;
				foreach (var attr in attrs)
				{
					if (attr is not SkipAttribute &&
					    attr is not System.Runtime.CompilerServices.CompilerGeneratedAttribute)
					{
						switch (attr)
						{
							case CaptionAttribute attribute:
								caption = attribute.Caption;
								break;
							case SectionAttribute sa:
							{
								if (section != null)
									root.Add(section);
								section = new Section(sa.Caption, sa.Footer);
								break;
							}
						}
					}
					else
						skip = true;
				}
				if (skip)
					continue;
				
				caption ??= MakeCaption(mi.Name);
				
				section ??= new Section();
				
				Element? element = null;
				if (mType == typeof (string)){
					PasswordAttribute? pa = null;
					AlignmentAttribute? align = null;
					EntryAttribute? ea = null;
					object? html = null;
					NSAction? invoke = null;
					bool multi = false;
					
					foreach (object attr in attrs)
					{
						switch (attr)
						{
							case PasswordAttribute attribute:
								pa = attribute;
								break;
							case EntryAttribute attribute:
								ea = attribute;
								break;
							case MultilineAttribute:
								multi = true;
								break;
							case HtmlAttribute:
								html = attr;
								break;
							case AlignmentAttribute attribute:
								align = attribute;
								break;
							case OnTapAttribute attribute:
							{
								string methodName = attribute.Method;
							
								if (callbacks == null){
									throw new Exception ("Your class contains [OnTap] attributes, but you passed a null object for `context' in the constructor");
								}
							
								var method = callbacks.GetType ().GetMethod (methodName);
								if (method == null)
									throw new Exception ("Did not find method " + methodName);
								invoke = delegate {
									method.Invoke (method.IsStatic ? null : callbacks, Array.Empty<object>());
								};
								break;
							}
						}
					}
					
					string? value = (string?) GetValue (mi, o);
					if (pa != null)
						element = new EntryElement (caption, pa.Placeholder, value, true);
					else if (ea != null)
						element = new EntryElement (caption, ea.Placeholder, value) { KeyboardType = ea.KeyboardType, AutocapitalizationType = ea.AutocapitalizationType, AutocorrectionType = ea.AutocorrectionType, ClearButtonMode = ea.ClearButtonMode };
					else if (multi)
						element = new MultilineElement (caption, value);
					else if (html != null) {
						Trace.Assert(value is not null);
						element = new HtmlElement(caption, value);
					}
					else {
						var sElement = new StringElement (caption, value);
						element = sElement;
						
						if (align != null)
							sElement.Alignment = align.Alignment;
					}
					
					if (invoke != null) {
						var strElement = (StringElement) element;
						strElement.Tapped += invoke;
						_handlerMappings.Add (strElement, invoke);
					}
				} else if (mType == typeof (float)){
					var floatElement = new FloatElement (null, null, (float) GetValue (mi, o)!);
					floatElement.Caption = caption;
					element = floatElement;
					
					foreach (object attr in attrs){
						if (attr is RangeAttribute ra){
							floatElement.MinValue = ra.Low;
							floatElement.MaxValue = ra.High;
							floatElement.ShowCaption = ra.ShowCaption;
						}
					}
				} else if (mType == typeof (bool)){
					bool checkbox = false;
					foreach (object attr in attrs){
						if (attr is CheckboxAttribute)
							checkbox = true;
					}
					
					if (checkbox)
						element = new CheckboxElement (caption, (bool) GetValue (mi, o)!);
					else
						element = new BooleanElement (caption, (bool) GetValue (mi, o)!);
				} else if (mType == typeof (DateTime)){
					var dateTime = (DateTime) GetValue (mi, o)!;
					bool asDate = false, asTime = false;
					
					foreach (object attr in attrs){
						if (attr is DateAttribute)
							asDate = true;
						else if (attr is TimeAttribute)
							asTime = true;
					}
					
					if (asDate)
						element = new DateElement (caption, dateTime);
					else if (asTime)
						element = new TimeElement (caption, dateTime);
					else
						 element = new DateTimeElement (caption, dateTime);
				} else if (mType.IsEnum){
					var cSection = new Section ();
					ulong eValue = Convert.ToUInt64 (GetValue (mi, o), null);
					int idx = 0;
					int selected = 0;
					
					foreach (var fi in mType.GetFields (BindingFlags.Public | BindingFlags.Static)){
						ulong v = Convert.ToUInt64 (GetValue (fi, null));
						
						if (v == eValue)
							selected = idx;

						cSection.Add (new RadioElement (Attribute.GetCustomAttribute(fi, typeof(CaptionAttribute)) is CaptionAttribute ca ? ca.Caption : MakeCaption (fi.Name)));
						idx++;
					}
					
					element = new RootElement (caption, new RadioGroup (null, selected)) { cSection };
				} else if (mType == typeof (UIImage)){
					element = new ImageElement ((UIImage) GetValue (mi, o)!);
				} else if (typeof (IEnumerable).IsAssignableFrom (mType)){
					var cSection = new Section ();
					int count = 0;
					
					if (lastRadioIndex == null)
						throw new Exception ("IEnumerable found, but no previous int found");
					foreach (var e in (IEnumerable) GetValue (mi, o)!){
						cSection.Add (new RadioElement (e?.ToString() ?? ""));
						count++;
					}
					int selected = (int) GetValue (lastRadioIndex, o)!;
					if (selected >= count || selected < 0)
						selected = 0;
					element = new RootElement (caption, new MemberRadioGroup (null, selected, lastRadioIndex)) { cSection };
					lastRadioIndex = null;
				} else if (typeof (int) == mType){
					foreach (object attr in attrs){
						if (attr is RadioSelectionAttribute){
							lastRadioIndex = mi;
							break;
						}
					}
				} else {
					var nested = GetValue (mi, o);
					if (nested != null){
						var newRoot = new RootElement (caption);
						Populate (callbacks, nested, newRoot);
						element = newRoot;
					}
				}
				
				if (element == null)
					continue;
				section.Add (element);
				_mappings [element] = new MemberAndInstance (mi, o);
			}
			root.Add (section);
		}
		
		[PublicAPI]
		class MemberRadioGroup : RadioGroup {
			public MemberInfo MI;
			
			public MemberRadioGroup (string? key, int selected, MemberInfo mi) : base (key, selected)
			{
				MI = mi;
			}
		}
		
		public void Dispose ()
		{
			Dispose (true);
		}
		
		protected virtual void Dispose (bool disposing)
		{
			if (disposing){
				// Dispose any [OnTap] handler associated to its element
				foreach (var strElement in _handlerMappings)
					strElement.Key.Tapped -= strElement.Value;
				_handlerMappings = new Dictionary<StringElement, Action>();

				foreach (var element in _mappings.Keys){
					element.Dispose ();
				}
				_mappings = new Dictionary<Element, MemberAndInstance>();
			}
		}
		
		public void Fetch ()
		{
			foreach (var dk in _mappings){
				Element element = dk.Key;
				MemberInfo mi = dk.Value.Member;
				object obj = dk.Value.Obj;
				
				switch (element)
				{
					case DateTimeElement timeElement:
						SetValue (mi, obj, timeElement.DateValue);
						break;
					case FloatElement floatElement:
						SetValue (mi, obj, floatElement.Value);
						break;
					case BooleanElement booleanElement:
						SetValue (mi, obj, booleanElement.Value);
						break;
					case CheckboxElement checkboxElement:
						SetValue (mi, obj, checkboxElement.Value);
						break;
					case EntryElement entryElement:
					{
						var entry = entryElement;
						entry.FetchValue ();
						SetValue (mi, obj, entry.Value);
						break;
					}
					case ImageElement imageElement:
						SetValue (mi, obj, imageElement.Value);
						break;
					case RootElement { Group: MemberRadioGroup group } re:
						SetValue (group.MI, obj, re.RadioSelected);
						break;
					case RootElement re:
					{
						if (re.Group is RadioGroup){
							var mType = GetTypeForMember (mi);
							Trace.Assert(mType is not null);
							var fi = mType.GetFields (BindingFlags.Public | BindingFlags.Static) [re.RadioSelected];
						
							SetValue (mi, obj, fi.GetValue (null));
						}

						break;
					}
				}
			}
		}
	}
}
