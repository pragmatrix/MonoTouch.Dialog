// Copyright 2010-2011 Miguel de Icaza
//
// Based on the TweetStation specific ImageStore
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
using System.IO;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

using Foundation;
using UIKit;

namespace MonoTouch.Dialog.Utilities 
{
	/// <summary>
	///    This interface needs to be implemented to be notified when an image
	///    has been downloaded.   The notification will happen on the UI thread.
	///    Upon notification, the code should call RequestImage again, this time
	///    the image will be loaded from the on-disk cache or the in-memory cache.
	/// </summary>
	public interface IImageUpdated {
		void UpdatedImage (Uri uri);
	}
	
	/// <summary>
	///   Network image loader, with local file system cache and in-memory cache
	/// </summary>
	/// <remarks>
	///   By default, using the static public methods will use an in-memory cache
	///   for 50 images and 4 megs total.   The behavior of the static methods 
	///   can be modified by setting the public DefaultLoader property to a value
	///   that the user configured.
	/// 
	///   The instance methods can be used to create different image loader with 
	///   different properties.
	///  
	///   Keep in mind that the phone does not have a lot of memory, and using
	///   the cache with the unlimited value (0) even with a number of items in
	///   the cache can consume memory very quickly.
	/// 
	///   Use the Purge method to release all the memory kept in the caches on
	///   low memory conditions, or when the application is sent to the background.
	/// </remarks>

	public class ImageLoader
	{
        public static readonly string BaseDir = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), "..");
		const int MaxRequests = 6;
		static readonly string PicDir; 
		
		// Cache of recently used images
		readonly LRUCache<Uri,UIImage> _cache;
		
		// A list of requests that have been issues, with a list of objects to notify.
		static readonly Dictionary<Uri, List<IImageUpdated>> PendingRequests;
		
		// A list of updates that have completed, we must notify the main thread about them.
		static readonly HashSet<Uri> QueuedUpdates;
		
		// A queue used to avoid flooding the network stack with HTTP requests
		static readonly Stack<Uri> RequestQueue;
		
		static readonly NSString NsDispatcher = new("x");
		
		static readonly MD5CryptoServiceProvider Checksum = new();
		
		/// <summary>
		///    This contains the default loader which is configured to be 50 images
		///    up to 4 megs of memory.   Assigning to this property a new value will
		///    change the behavior.   This property is lazily computed, the first time
		///    an image is requested.
		/// </summary>
		public static ImageLoader? DefaultLoader;
		
		static ImageLoader ()
		{
			PicDir = Path.Combine (BaseDir, "Library/Caches/Pictures.MonoTouch.Dialog/");
			
			if (!Directory.Exists (PicDir))
				Directory.CreateDirectory (PicDir);
			
			PendingRequests = new Dictionary<Uri,List<IImageUpdated>> ();
			QueuedUpdates = new HashSet<Uri>();
			RequestQueue = new Stack<Uri> ();
		}
		
		/// <summary>
		///   Creates a new instance of the image loader
		/// </summary>
		/// <param name="cacheSize">
		/// The maximum number of entries in the LRU cache
		/// </param>
		/// <param name="memoryLimit">
		/// The maximum number of bytes to consume by the image loader cache.
		/// </param>
		public ImageLoader (int cacheSize, int memoryLimit)
		{
			_cache = new LRUCache<Uri, UIImage> (cacheSize, memoryLimit, Sizer);
		}
		
		static int Sizer (UIImage img)
		{
			var cg = img.CGImage;
			Trace.Assert(cg is not null);
			return (int)(cg.BytesPerRow * cg.Height);
		}
		
		/// <summary>
		///    Purges the contents of the DefaultLoader
		/// </summary>
		public static void Purge ()
		{
			if (DefaultLoader != null)
				DefaultLoader.PurgeCache ();
		}
		
		/// <summary>
		///    Purges the cache of this instance of the ImageLoader, releasing 
		///    all the memory used by the images in the caches.
		/// </summary>
		public void PurgeCache ()
		{
			lock (_cache)
				_cache.Purge ();
		}
		
		static int Hex (int v)
		{
			if (v < 10)
				return '0' + v;
			return 'a' + v-10;
		}

		static string Md5 (string input)
		{
			var bytes = Checksum.ComputeHash (Encoding.UTF8.GetBytes (input));
			var ret = new char [32];
			for (int i = 0; i < 16; i++){
				ret [i*2] = (char)Hex (bytes [i] >> 4);
				ret [i*2+1] = (char)Hex (bytes [i] & 0xf);
			}
			return new string (ret);
		}
		
		/// <summary>
		///   Requests an image to be loaded using the default image loader
		/// </summary>
		/// <param name="uri">
		/// The URI for the image to load
		/// </param>
		/// <param name="notify">
		/// A class implementing the IImageUpdated interface that will be invoked when the image has been loaded
		/// </param>
		/// <returns>
		/// If the image has already been downloaded, or is in the cache, this will return the image as a UIImage.
		/// </returns>
		public static UIImage? DefaultRequestImage (Uri uri, IImageUpdated notify)
		{
			DefaultLoader ??= new ImageLoader(50, 4 * 1024 * 1024);
			return DefaultLoader.RequestImage (uri, notify);
		}
		
		/// <summary>
		///   Requests an image to be loaded from the network
		/// </summary>
		/// <param name="uri">
		/// The URI for the image to load
		/// </param>
		/// <param name="notify">
		/// A class implementing the IImageUpdated interface that will be invoked when the image has been loaded
		/// </param>
		/// <returns>
		/// If the image has already been downloaded, or is in the cache, this will return the image as a UIImage.
		/// </returns>
		public UIImage? RequestImage (Uri uri, IImageUpdated notify)
		{
			UIImage? ret;
			
			lock (_cache){
				ret = _cache [uri];
				if (ret != null)
					return ret;
			}

			lock (RequestQueue){
				if (PendingRequests.ContainsKey (uri)) {
					if (!PendingRequests [uri].Contains(notify))
						PendingRequests [uri].Add (notify);
					return null;
				}				
			}

			var picFile = uri.IsFile ? uri.LocalPath : PicDir + Md5 (uri.AbsoluteUri);
			if (File.Exists (picFile)){
				ret = UIImage.FromFile (picFile);
				if (ret != null){
					lock (_cache)
						_cache [uri] = ret;
					return ret;
				}
			} 
			if (uri.IsFile)
				return null;
			QueueRequest (uri, notify);
			return null;
		}
		
		static void QueueRequest (Uri uri, IImageUpdated notify)
		{
			if (notify == null)
				throw new ArgumentNullException (nameof(notify));
			
			lock (RequestQueue){
				if (PendingRequests.TryGetValue(uri, out var request)){
					//Util.Log ("pendingRequest: added new listener for {0}", id);
					request.Add (notify);
					return;
				}
				var slot = new List<IImageUpdated> (4) { notify };
				PendingRequests [uri] = slot;
				
				if (_picDownloaders >= MaxRequests)
					RequestQueue.Push (uri);
				else {
					ThreadPool.QueueUserWorkItem (delegate { 
							try {
								StartPicDownload (uri); 
							} catch (Exception e){
								Console.WriteLine (e);
							}
					});
				}
			}
		}
		
		static bool Download (Uri uri)
		{
			try {
				var target =  PicDir + Md5 (uri.AbsoluteUri);
				var req = new NSUrlRequest (new NSUrl (uri.AbsoluteUri), NSUrlRequestCachePolicy.UseProtocolCachePolicy, 120);
				var data = NSUrlConnection.SendSynchronousRequest (req, out _, out _);
				return data.Save (target, true, out _);
			} catch (Exception e) {
				Console.WriteLine ("Problem with {0} {1}", uri, e);
				return false;
			}
		}
		
		static long _picDownloaders;
		
		static void StartPicDownload (Uri uri)
		{
			Interlocked.Increment (ref _picDownloaders);
			try {
				_StartPicDownload (uri);
			} catch (Exception e){
				Console.Error.WriteLine ("CRITICAL: should have never happened {0}", e);
			}
			//Util.Log ("Leaving StartPicDownload {0}", pictureDownloaders);
			Interlocked.Decrement (ref _picDownloaders);
		}
		
		static void _StartPicDownload (Uri uriToDownload)
		{
			Uri? uri = uriToDownload;
			do {
				//System.Threading.Thread.Sleep (5000);
				bool downloaded = Download (uri);
				//if (!downloaded)
				//	Console.WriteLine ("Error fetching picture for {0} to {1}", uri, target);
				
				// Cluster all updates together
				bool doInvoke = false;
				
				lock (RequestQueue)
				{
					if (downloaded){
						QueuedUpdates.Add (uri);
					
						// If this is the first queued update, must notify
						if (QueuedUpdates.Count == 1)
							doInvoke = true;
					} else
						PendingRequests.Remove (uri);

					// Try to get more jobs.
					uri = RequestQueue.Count > 0 ? RequestQueue.Pop () :
						// if (uri == null){
						// 	Console.Error.WriteLine ("Dropping request {0} because url is null", uri);
						// 	pendingRequests.Remove (uri);
						// 	uri = null;
						// }
						//Util.Log ("Leaving because requestQueue.Count = {0} NOTE: {1}", requestQueue.Count, pendingRequests.Count);
						null;
				}	
				if (doInvoke)
					NsDispatcher.BeginInvokeOnMainThread (NotifyImageListeners);
				
			} while (uri != null);
		}
		
		// Runs on the main thread
		static void NotifyImageListeners ()
		{
			lock (RequestQueue){
				foreach (var qUri in QueuedUpdates){
					var list = PendingRequests [qUri];
					PendingRequests.Remove (qUri);
					foreach (var pr in list){
						try {
							pr.UpdatedImage (qUri);
						} catch (Exception e){
							Console.WriteLine (e);
						}
					}
				}
				QueuedUpdates.Clear ();
			}
		}
	}
}
