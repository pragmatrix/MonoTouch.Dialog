//
// A simple LRU cache used for tracking the images
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// Copyright 2010 Miguel de Icaza
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace MonoTouch.Dialog.Utilities {
	
[PublicAPI]
public class LRUCache<TKey, TValue>
	where TKey : notnull
	where TValue : class?, IDisposable? 
	
{
	readonly Dictionary<TKey, LinkedListNode <TValue>> _dict;
	readonly Dictionary<LinkedListNode<TValue>, TKey> _revDict;
	readonly LinkedList<TValue> _list;
	readonly int _entryLimit;
	readonly int _sizeLimit;
	int _currentSize;
	readonly Func<TValue,int>? _slotSizeFunc;

	public LRUCache (int entryLimit, int sizeLimit = 0, Func<TValue,int>? slotSizer = null)
	{
		_list = new LinkedList<TValue> ();
		_dict = new Dictionary<TKey, LinkedListNode<TValue>> ();
		_revDict = new Dictionary<LinkedListNode<TValue>, TKey> ();
		
		if (sizeLimit != 0 && slotSizer == null)
			throw new ArgumentNullException (nameof(slotSizer), "If sizeLimit is set, the slotSizer must be provided");
		
		_entryLimit = entryLimit;
		_sizeLimit = sizeLimit;
		_slotSizeFunc = slotSizer;
	}

	void Evict ()
	{
		var last = _list.Last;
		Trace.Assert(last is not null);
		var key = _revDict [last];
		
		if (_sizeLimit > 0){
			var size = _slotSizeFunc!(last.Value);
			_currentSize -= size;
		}
		
		_dict.Remove (key);
		_revDict.Remove (last);
		_list.RemoveLast ();
		last.Value!.Dispose ();
	}

	public void Purge ()
	{
		foreach (var element in _list)
			element!.Dispose ();
		
		_dict.Clear ();
		_revDict.Clear ();
		_list.Clear ();
		_currentSize = 0;
	}

	[DisallowNull]
	public TValue? this [TKey key] {
		get {
			if (_dict.TryGetValue (key, out var node)){
				_list.Remove (node);
				_list.AddFirst (node);

				return node.Value;
			}
			return null;
		}

		set {
			var size = _sizeLimit > 0 ? _slotSizeFunc! (value) : 0;
			
			if (_dict.TryGetValue (key, out LinkedListNode<TValue>? node)){
				if (_sizeLimit > 0 && node.Value != null){
					int repSize = _slotSizeFunc! (node.Value);
					_currentSize -= repSize;
					_currentSize += size;
				}
				
				// If we already have a key, move it to the front
				_list.Remove (node);
				_list.AddFirst (node);
	
				// Remove the old value
				if (node.Value != null)
					node.Value.Dispose ();
				node.Value = value;
				while (_sizeLimit > 0 && _currentSize > _sizeLimit && _list.Count > 1)
					Evict ();
				return;
			}
			if (_sizeLimit > 0){
				while (_sizeLimit > 0 && _currentSize + size > _sizeLimit && _list.Count > 0)
					Evict ();
			}
			if (_dict.Count >= _entryLimit)
				Evict ();
			// Adding new node
			node = new LinkedListNode<TValue> (value);
			_list.AddFirst (node);
			_dict [key] = node;
			_revDict [node] = key;
			_currentSize += size;
		}
	}

	public override string ToString ()
	{
		return "LRUCache dict={0} revdict={1} list={2}";
	}		
}
}
