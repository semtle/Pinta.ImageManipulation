/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) Rick Brewster, Tom Jackson, and past contributors.            //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pinta.ImageManipulation
{
	/// <summary>
	/// Defines a way to operate on a pixel, or a region of pixels, in a unary fashion.
	/// That is, it is a simple function F that takes one parameter and returns a
	/// result of the form: d = F(c)
	/// </summary>
	public unsafe abstract class UnaryPixelOp : PixelOp
	{
		public abstract ColorBgra Apply (ColorBgra color);

		public void Apply (ISurface surface)
		{
			Apply (surface, surface.Bounds);
		}

		public void Apply (ISurface surface, Rectangle roi)
		{
			ApplyLoop (surface, roi, CancellationToken.None, null);
		}

		public void Apply (ISurface src, ISurface dst)
		{
			if (src.Bounds != dst.Bounds)
				throw new InvalidOperationException ("Source and destination surfaces must be the same size or use an overload with a specified bounds.");

			Apply (src, dst, src.Bounds);
		}

		public void Apply (ISurface src, ISurface dst, Rectangle roi)
		{
			ApplyLoop (src, dst, roi, CancellationToken.None, null);
		}

		public Task ApplyAsync (ISurface surface)
		{
			return ApplyAsync (surface, surface.Bounds, CancellationToken.None);
		}

		public Task ApplyAsync (ISurface surface, CancellationToken token)
		{
			return ApplyAsync (surface, surface.Bounds, token);
		}

		public Task ApplyAsync (ISurface surface, CancellationToken token, IRenderProgress progress)
		{
			return ApplyAsync (surface, surface.Bounds, token, progress);
		}

		public Task ApplyAsync (ISurface surface, Rectangle roi)
		{
			return ApplyAsync (surface, roi, CancellationToken.None);
		}

		public Task ApplyAsync (ISurface surface, Rectangle roi, CancellationToken token)
		{
			return Task.Factory.StartNew (() => ApplyLoop (surface, surface.Bounds, token, null));
		}

		public Task ApplyAsync (ISurface surface, Rectangle roi, CancellationToken token, IRenderProgress progress)
		{
			return Task.Factory.StartNew (() => ApplyLoop (surface, surface.Bounds, token, progress));
		}

		public Task ApplyAsync (ISurface src, ISurface dst)
		{
			if (src.Bounds != dst.Bounds)
				throw new InvalidOperationException ("Source and destination surfaces must be the same size or use an overload with a specified bounds.");

			return ApplyAsync (src, dst, src.Bounds, CancellationToken.None);
		}

		public Task ApplyAsync (ISurface src, ISurface dst, CancellationToken token)
		{
			if (src.Bounds != dst.Bounds)
				throw new InvalidOperationException ("Source and destination surfaces must be the same size or use an overload with a specified bounds.");

			return ApplyAsync (src, dst, src.Bounds, token);
		}

		public Task ApplyAsync (ISurface src, ISurface dst, CancellationToken token, IRenderProgress progress)
		{
			if (src.Bounds != dst.Bounds)
				throw new InvalidOperationException ("Source and destination surfaces must be the same size or use an overload with a specified bounds.");

			return ApplyAsync (src, dst, src.Bounds, token, progress);
		}

		public Task ApplyAsync (ISurface src, ISurface dst, Rectangle roi)
		{
			return ApplyAsync (src, dst, roi, CancellationToken.None);
		}

		public Task ApplyAsync (ISurface src, ISurface dst, Rectangle roi, CancellationToken token)
		{
			return Task.Factory.StartNew (() => ApplyLoop (src, dst, roi, token, null));
		}

		public Task ApplyAsync (ISurface src, ISurface dst, Rectangle roi, CancellationToken token, IRenderProgress progress)
		{
			return Task.Factory.StartNew (() => ApplyLoop (src, dst, roi, token, progress));
		}

		public unsafe virtual void Apply (ColorBgra* ptr, int length)
		{
			unsafe {
				while (length > 0) {
					*ptr = Apply (*ptr);
					++ptr;
					--length;
				}
			}
		}

		public unsafe override void Apply (ColorBgra* src, ColorBgra* dst, int length)
		{
			unsafe {
				while (length > 0) {
					*dst = Apply (*src);
					++dst;
					++src;
					--length;
				}
			}
		}

		protected void ApplyLoop (ISurface src, Rectangle roi, CancellationToken token, IRenderProgress progress)
		{
			src.BeginUpdate ();

			var completed_lines = new bool[roi.Height];
			var last_completed_index = 0;

			if (Settings.SingleThreaded || roi.Height <= 1) {
				for (var y = roi.Y; y <= roi.Bottom; ++y) {
					if (token.IsCancellationRequested)
						return;

					var dstPtr = src.GetPointAddress (roi.X, y);
					Apply (dstPtr, roi.Width);

					completed_lines[y - roi.Top] = true;

					if (progress != null) {
						var last_y = FindLastCompletedLine (completed_lines, last_completed_index);
						last_completed_index = last_y;
						progress.CompletedRoi = new Rectangle (roi.X, roi.Y, roi.Width, last_y);
						progress.PercentComplete = (float)last_y / (float)roi.Height;
					}
				}
			} else {
				ParallelExtensions.OrderedFor (roi.Y, roi.Bottom + 1, token, (y) => {
					var dstPtr = src.GetPointAddress (roi.X, y);
					Apply (dstPtr, roi.Width);

					completed_lines[y - roi.Top] = true;

					if (progress != null) {
						var last_y = FindLastCompletedLine (completed_lines, last_completed_index);
						last_completed_index = last_y;
						progress.CompletedRoi = new Rectangle (roi.X, roi.Y, roi.Width, last_y);
						progress.PercentComplete = (float)last_y / (float)roi.Height;
					}
				});
			}

			src.EndUpdate ();
		}

		protected void ApplyLoop (ISurface src, ISurface dst, Rectangle roi, CancellationToken token, IRenderProgress progress)
		{
			src.BeginUpdate ();
			dst.BeginUpdate ();

			var completed_lines = new bool[roi.Height];
			var last_completed_index = 0;

			if (Settings.SingleThreaded || roi.Height <= 1) {
				for (var y = roi.Y; y <= roi.Bottom; ++y) {
					if (token.IsCancellationRequested)
						return;

					var dstPtr = dst.GetPointAddress (roi.X, y);
					var srcPtr = src.GetPointAddress (roi.X, y);
					Apply (srcPtr, dstPtr, roi.Width);

					completed_lines[y - roi.Top] = true;

					if (progress != null) {
						var last_y = FindLastCompletedLine (completed_lines, last_completed_index);
						last_completed_index = last_y;
						progress.CompletedRoi = new Rectangle (roi.X, roi.Y, roi.Width, last_y);
						progress.PercentComplete = (float)last_y / (float)roi.Height;
					}
				}
			} else {
				ParallelExtensions.OrderedFor (roi.Y, roi.Bottom + 1, token, (y) => {
					var dstPtr = dst.GetPointAddress (roi.X, y);
					var srcPtr = src.GetPointAddress (roi.X, y);
					Apply (srcPtr, dstPtr, roi.Width);

					completed_lines[y - roi.Top] = true;

					if (progress != null) {
						var last_y = FindLastCompletedLine (completed_lines, last_completed_index);
						last_completed_index = last_y;
						progress.CompletedRoi = new Rectangle (roi.X, roi.Y, roi.Width, last_y);
						progress.PercentComplete = (float)last_y / (float)roi.Height;
					}
				});
			}

			src.EndUpdate ();
			dst.EndUpdate ();
		}

		// We always want to return a contiguous roi of lines completed, even
		// if it means we don't report some lines that we've already completed.
		private int FindLastCompletedLine (bool[] lines, int start)
		{
			for (var i = start; i < lines.Length; i++)
				if (!lines[i])
					return Math.Max (i - 1, 0);

			return lines.Length - 1;
		}
	}
}
