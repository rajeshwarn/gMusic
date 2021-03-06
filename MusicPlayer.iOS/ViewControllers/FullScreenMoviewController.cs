﻿using System;
using System.Collections.Generic;
using System.Text;
using Foundation;
using MusicPlayer.iOS.UI;
using MusicPlayer.Managers;
using MusicPlayer.Models;
using UIKit;
using SimpleTables;
using System.Timers;

namespace MusicPlayer.iOS
{
	[Register("FullScreenMovieController")]
	public class FullScreenMovieController : UIViewController
	{
		//UIButton button;
		public FullScreenMovieController()
		{
			ModalTransitionStyle = UIModalTransitionStyle.CrossDissolve;
		}

		public FullScreenMovieController(IntPtr handle)
			: base(handle)
		{

		}

		FullScreenVideoView view;

		public override void LoadView()
		{
			View = view = new FullScreenVideoView {
				Parent = this,
			};
			//View.AddSubview(button = new UIButton());
			//button.BackgroundColor = UIColor.Black.ColorWithAlpha(0);
			//button.SetTitleColor(UIColor.White.ColorWithAlpha(.25f), UIControlState.Normal);
			//button.SetTitle("", UIControlState.Normal);

		}

		public override void ViewWillAppear(bool animated)
		{
			base.ViewWillAppear(animated);
			//button.TouchUpInside += HandleTouchUpInside;
			view.Show();
			UIApplication.SharedApplication.SetStatusBarHidden(true, animated);
			NotificationManager.Shared.VideoPlaybackChanged += SharedOnVideoPlaybackChanged;
			NotificationManager.Shared.PlaybackStateChanged += PlaybackStateChanged;
			NotificationManager.Shared.CurrentTrackPositionChanged += PlaybackTimeChanged;
			view.SetPlaybackState(PlaybackManager.Shared.NativePlayer.State == PlaybackState.Playing);
			//if (InterfaceOrientation.IsPortrait())
			//	UIDevice.CurrentDevice.SetValueForKey(NSNumber.FromInt32((int)UIInterfaceOrientation.LandscapeLeft),(NSString)"orientation");

		}

		void PlaybackStateChanged (object sender, EventArgs<PlaybackState> e)
		{
			view.SetPlaybackState(e.Data == PlaybackState.Playing);
		}


		void PlaybackTimeChanged (object sender, EventArgs<TrackPosition> e)
		{
			view.SetPlaybackPosition(e.Data);
		}

		void SharedOnVideoPlaybackChanged(object sender, EventArgs<bool> eventArgs)
		{
			if (eventArgs.Data)
				NotificationManager.Shared.ProcToggleFullScreenVideo();
			else
				view.Show();
		}

		public override void ViewWillDisappear(bool animated)
		{
			base.ViewWillDisappear(animated);
			//button.TouchUpInside -= HandleTouchUpInside;
			NotificationManager.Shared.VideoPlaybackChanged -= SharedOnVideoPlaybackChanged;
			NotificationManager.Shared.PlaybackStateChanged -= PlaybackStateChanged;
			NotificationManager.Shared.CurrentTrackPositionChanged -= PlaybackTimeChanged;
			UIApplication.SharedApplication.SetStatusBarHidden(false, animated);
		}

		[Register("FullScreenVideoView")]
		public class FullScreenVideoView : UIView
		{
			WeakReference parent;

			public FullScreenMovieController Parent {
				get { return parent?.Target as FullScreenMovieController; }
				set { parent = new WeakReference(value); }
			}

			VideoView videoView;
			UIToolbar topToolbar;
			UIToolbar bottomToolbar;
			SimpleButton playPauseButton;
			UILabel timeLabel;
			UILabel timeRemaingLabel;
			ProgressView slider;
			bool visible;

			public FullScreenVideoView()
			{

				TintColor = UIColor.White;
				Add(videoView = new VideoView {
					Tapped = Toggle
				});

				Add(topToolbar = new UIToolbar {
					BackgroundColor = UIColor.Clear,
					Items = new UIBarButtonItem [] {
						new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
						new UIBarButtonItem(UIBarButtonSystemItem.Done, (s, e) => {
							Parent.DismissViewControllerAsync(true);
						}),
						new UIBarButtonItem(UIBarButtonSystemItem.FixedSpace) { Width = 10 },
					}
				});
				topToolbar.SetBackgroundImage(new UIImage(), UIToolbarPosition.Any, UIBarMetrics.Default);
				topToolbar.SetShadowImage(new UIImage(), UIToolbarPosition.Any);
				topToolbar.SizeToFit();
				Add(playPauseButton = new SimpleButton{
					BackgroundColor = UIColor.Black.ColorWithAlpha(.5f),
					Frame = new CoreGraphics.CGRect(0,0,50,50),
					Layer = {
						CornerRadius = 25,
					},
					Tapped = (b)=> PlaybackManager.Shared.PlayPause(),

				});
				timeLabel = new UILabel{Text = "0000:00"}.StyleAsSubText();
				timeLabel.TextColor = UIColor.White;
				timeLabel.SizeToFit();
				timeRemaingLabel = new UILabel {Text = "0000:00", TextAlignment = UITextAlignment.Right}.StyleAsSubText();
				timeRemaingLabel.TextColor = UIColor.White;
				timeRemaingLabel.SizeToFit();
				slider = new ProgressView();
				slider.EditingStarted = () => timer.Stop();
				slider.EditingEnded = () => ResetTimer();
				Add(bottomToolbar = new UIToolbar {

					BackgroundColor = UIColor.Clear,
					Items = new UIBarButtonItem[] {
						new UIBarButtonItem(UIBarButtonSystemItem.FixedSpace) { Width = 10 },
						new UIBarButtonItem(timeLabel),

						new UIBarButtonItem(slider),

						new UIBarButtonItem(timeRemaingLabel),
						new UIBarButtonItem(UIBarButtonSystemItem.FixedSpace) { Width = 10 },
					}
				});

				bottomToolbar.SetBackgroundImage(new UIImage(), UIToolbarPosition.Any, UIBarMetrics.Default);
				bottomToolbar.SetShadowImage(new UIImage(), UIToolbarPosition.Any);
				bottomToolbar.SizeToFit();
				timer = new Timer(5000);
				timer.Elapsed += Timer_Elapsed;
			}

			private void Timer_Elapsed(object sender, ElapsedEventArgs e)
			{
				App.RunOnMainThread(() => hideOverlay());
			}

			public void Toggle()
			{
				if (visible)
					hideOverlay();
				else
					showOverlay();
			}

			public void SetPlaybackState(bool playing)
			{
				playPauseButton.Image = playing ? Images.GetPauseButton(25) : Images.GetPlaybackButton(25);
				if (!playing) {
					showOverlay(false);
					timer.Stop();
				} else
					ResetTimer();
			}

			internal void SetPlaybackPosition(TrackPosition data)
			{
				slider.SliderProgress = data.Percent;
				timeLabel.Text = data.CurrentTimeString;
				timeRemaingLabel.Text = data.RemainingTimeString;
			}

			async void showOverlay(bool autohide = true)
			{
				if (visible)
					return;

				visible = true;
				await AnimateAsync(.2, () => {
					var bounds = Bounds;
					var frame = topToolbar.Frame;
					frame.Y = 0;
					topToolbar.Frame = frame;

					playPauseButton.Alpha = 1;

					frame = bottomToolbar.Frame;
					frame.Y = bounds.Height - frame.Height;
					bottomToolbar.Frame = frame;
				});
				if (autohide)
					ResetTimer();
				else
					timer?.Stop();
			}

			void hideOverlay()
			{
				if (!visible)
					return;
				visible = false;
				AnimateAsync(.2, () => {
					var bounds = Bounds;
					var frame = topToolbar.Frame;
					frame.Y = -frame.Height;
					topToolbar.Frame = frame;

					playPauseButton.Alpha = 0;

					frame = bottomToolbar.Frame;
					frame.Y = bounds.Height;
					bottomToolbar.Frame = frame;
				});
			}

			public void Show()
			{
				videoView.Show();
				showOverlay();
			}

			Timer timer;

			public void ResetTimer()
			{
				timer.Stop();
				timer.Start();
			}

			public override void LayoutSubviews()
			{
				base.LayoutSubviews();
				videoView.Frame = Bounds;
				var bounds = Bounds;

				playPauseButton.Center = bounds.GetCenter();

				var frame = topToolbar.Frame;
				frame.Width = bounds.Width;
				frame.Y = visible ? 0 : -frame.Height;
				topToolbar.Frame = frame;

				frame = slider.Frame;
				//Custom views have a padding of 12, we have 3 custom views (labels, slider);
				frame.Width = bounds.Width - 20  - (timeLabel.Frame.Width * 2)- (12 * bottomToolbar.Items.Length);
				slider.Frame = frame;
				
				frame = bottomToolbar.Frame;
				frame.Width = bounds.Width;
				frame.Y = visible ? bounds.Height - frame.Height : bounds.Height;
				bottomToolbar.Frame = frame;

				ResetTimer();
			}
		}
		
	}
}

