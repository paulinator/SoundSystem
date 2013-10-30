using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Resources;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace PThomann.Utilities.SoundSystem
{
	public delegate bool BoolDelegate();

	/// <summary>
	/// Tool class that helps with audio for Silverlight for Windows Phone Apps.
	/// This class behaves according to validation requirements regarding music,
	/// ie. on Enter() it asks the users whether they want to stop playback if music is already running.
	/// Uses MediaElement for playing music, and Xna SoundEffect to play Effects.
	/// Creates an Xna game timer on Enter() and stops it on Leave().
	/// SoundEffects are stored for reuse and can be 'manually' unloaded.
	/// </summary>
	public static class SoundSystem
	{
		static SoundSystem()
		{
			music = new MediaElement();
			music.MediaOpened += music_MediaOpened;
			music.MediaEnded += music_MediaEnded;
			music.MarkerReached += music_MarkerReached;

			// Timer to simulate the XNA game loop (SoundEffect class is from the XNA Framework)
			gameTimer = new GameTimer();
			gameTimer.UpdateInterval = TimeSpan.FromMilliseconds(33);

			// Call FrameworkDispatcher.Update to update the XNA Framework internals.
			gameTimer.Update += delegate { try { FrameworkDispatcher.Update(); } catch { } };
			
			gameTimer.Start();

			// Prime the pump or we'll get an exception.
			FrameworkDispatcher.Update();
		}

		#region private

		static string loadedMusicFileUri;
		static TimeSpan musicStart;

		private static object threadLock = new Object();  // to be used in all public code for locking

		private static MediaElement music;
		private static Popup popup;

		private static bool canPlay, entered, resumeMediaPlayerAfterDone;
		private static Dictionary<string, SoundEffect> effects = new Dictionary<string, SoundEffect>();
		private static BoolDelegate askUserDelegate;
		private static GameTimer gameTimer;

		private static bool askUser()
		{
			if (askUserDelegate != null)
				return askUserDelegate.Invoke();
			else
				return (MessageBoxResult.OK == MessageBox.Show(
					"You are currently playing music from your library.\r\n\r\nDo you wish to pause the music in order to listen to the games own music?\r\n\r\nPress Cancel if you wish to continue listening to the current music while playing.",
					"Music Choice", MessageBoxButton.OKCancel));
		}

		static void music_MediaOpened(object sender, RoutedEventArgs e)
		{
			if (music.CanSeek)
				music.Position = musicStart;
			Action action = MediaOpenedAction;
			if (action != null)
			{
				action();
			}
		}
		private static void music_MediaEnded(object sender, RoutedEventArgs e)
		{
			lock (threadLock)
			{
				Action endAction = MediaEndedAction;
				if (endAction != null)
				{
					endAction();
				}
			}
		}

		static void music_MarkerReached(object sender, TimelineMarkerRoutedEventArgs e)
		{
			Action<TimelineMarker> ma = MarkerReachedAction;
			if (ma != null)
				ma(e.Marker);
		}

		#endregion private

		#region Properties

		/// <summary>
		/// Is triggered when the music reaches its end (MediaEnded).
		/// </summary>
		public static Action MediaEndedAction { get; set; }
		/// <summary>
		/// Is triggered when a music file finished loading (MediaOpened).
		/// </summary>
		public static Action MediaOpenedAction { get; set; }
		/// <summary>
		/// Is triggered whenever a TimelineMarker is reached during music playback.
		/// You add/remove Markers using SoundSystem.MusicMarkers.
		/// </summary>
		public static Action<TimelineMarker> MarkerReachedAction { get; set; }

		/// <summary>
		/// A Delegate taking no Arguments and returning a boolean value.
		/// If this is set, it overrides the default MessageBox asking 
		/// what to do if music is already playing.
		/// In order for your app to pass validation, any delegate you set this to
		/// must get feedback from the user if they want to interrupt the music for the app.
		/// </summary>
		public static BoolDelegate AskUserDelegate
		{
			get { lock (threadLock) { return askUserDelegate; } }
			set { lock (threadLock) { askUserDelegate = value; } }
		}

		/// <summary>
		/// Indicates if the SoundSystem has been Entered, which means Enter() has been called.
		/// To play music using SoundSystem, it needs to be Entered.
		/// </summary>
		public static bool Entered
		{
			get { lock (threadLock) { return entered; } }
		}

		/// <summary>
		/// Indicates whether it is possible to play music.
		/// This is only false if there was music running 
		/// before starting the app, and the user decided against interrupting it.
		/// 
		/// Note that this does not concern sound effects, they can always be played.
		/// </summary>
		public static bool MusicCanPlay
		{
			get { lock (threadLock) { return canPlay; } }
		}
		/// <summary>
		/// Indicates or sets the music volume. Values from 0 (quiet) to 1.
		/// </summary>
		public static double MusicVolume
		{
			get { lock (threadLock) { return music.Volume; } }
			set { lock (threadLock) { music.Volume = value; } }
		}
		/// <summary>
		/// Indicates or sets the music balance. values from -1 (L) to 1 (R).
		/// </summary>
		public static double MusicBalance
		{
			get { lock (threadLock) { return music.Balance; } }
			set { lock (threadLock) { music.Balance = value; } }
		}
		/// <summary>
		/// Indicates or sets whether the music playback is muted (but continues nonetheless)
		/// </summary>
		public static bool MusicIsMuted
		{
			get { lock (threadLock) { return music.IsMuted; } }
			set { lock (threadLock) { music.IsMuted = value; } }
		}
		/// <summary>
		/// Indicates or sets if playback is paused.
		/// Note that setting this to Paused will only have an effect if SoundSystem.MusicCanPause.
		/// </summary>
		public static MediaElementState MusicState
		{
			get { lock (threadLock) { return music.CurrentState; } }
			set
			{
				lock (threadLock)
				{
					switch (value)
					{
						case MediaElementState.Paused:
							if (music.CanPause)
								music.Pause();
							break;
						case MediaElementState.Playing:
							music.Play();
							break;
						case MediaElementState.Stopped:
							music.Stop();
							break;
						default:
							break;
					}
				}
			}
		}
		/// <summary>
		/// Gets or sets the music playback position.
		/// Note that setting this will only have an effect if SoundSystem.MusicCanSeek.
		/// </summary>
		public static TimeSpan MusicPosition
		{
			get { lock (threadLock) { return music.Position; } }
			set { lock (threadLock) { if (music.CanSeek) music.Position = value; } }
		}
		/// <summary>
		/// Indicates if music can be paused.
		/// </summary>
		public static bool MusicCanPause
		{
			get { lock (threadLock) { return music.CanPause; } }
		}
		/// <summary>
		/// Indicates if playback position can be changed deliberately.
		/// </summary>
		public static bool MusicCanSeek
		{
			get { lock (threadLock) { return music.CanSeek; } }
		}
		/// <summary>
		/// Indicates the total duration of the currently playing music file.
		/// </summary>
		public static Duration MusicFileDuration
		{
			get {lock(threadLock){ return music.NaturalDuration;} }
		}
		/// <summary>
		/// The MediaElement.Markers collection, which is used to add TimelineMarkers that will trigger the MarkerReachedAction.
		/// NOTE: whenever a sound file is loaded, all markers are cleared. 
		/// In order for your markers to still be there when the music plays, you must set them in the MediaOpenedAction (or later).
		/// </summary>
		public static TimelineMarkerCollection MusicMarkers { 
			get { return music.Markers; } 
		}

		/// <summary>
		/// Global Effects Master volume.
		/// </summary>
		public static float EffectsVolume
		{
			get { lock (threadLock) { return SoundEffect.MasterVolume; } }
			set { lock (threadLock) { SoundEffect.MasterVolume = value; } }
		}

		public static bool ZunePaused { get { return resumeMediaPlayerAfterDone; } }

		#endregion Properties

		#region Methods

		/// <summary>
		/// This enters the app, soundwise.
		/// Playback mechanisms are initialized, and if necessary it is determined 
		/// whether music can be played.
		/// 
		/// It is usually best to include this in the Application Launching and Activated events.
		/// </summary>
		public static void Enter()
		{
			lock (threadLock)
			{
				if (entered)
					return;
				entered = true;

				// Pause the Zune player if it is already playing music.
				// TODO: ask user if music playing from the library can be stopped
				if (!MediaPlayer.GameHasControl)
				{
					bool b = askUser();
					if (b)
					{
						canPlay = true;
						MediaPlayer.Pause();
						resumeMediaPlayerAfterDone = true;
					}
					else
					{
						entered = false;
						canPlay = false;
					}
				}
				else // (MediaPlayer.GameHasControl)
					canPlay = true;

				if (canPlay)
				{
					if (popup == null)
					{
						popup = new Popup();
						popup.Child = music;
						popup.Opacity = 0;
						popup.Height = 0;
						popup.Width = 0;
						music.Visibility = Visibility.Collapsed;
					}
					popup.IsOpen = true;
				} 
			}
		}

		/// <summary>
		/// This is the same as Enter(), but disables user prompt if force is true.
		/// Useful if you implement your own user prompt which is non-blocking.
		/// You'd call Enter(), return false upon showing your prompt via AskUserDelegate, 
		/// and later call Enter(true) when and if the user decides so.
		/// If you use this to avoid asking the User, your app will be rejected by Microsoft.
		/// </summary>
		/// <param name="force">assume the user said YES?</param>
		public static void Enter(bool force)
		{
			BoolDelegate tmp = askUserDelegate;
			if (force)
			{
				askUserDelegate = () => { return true; };
			}
			Enter();
			askUserDelegate = tmp;
		}
		/// <summary>
		/// Closes the resources used to play sound and 
		/// if appropriate continues Zune where it was before starting the app.
		/// 
		/// It seems best to include this in the Application Closing and Deactivated events.
		/// </summary>
		public static void Leave()
		{
			lock (threadLock)
			{
				if (!entered)
					return;
				entered = false;

				if (music != null && music.CurrentState == MediaElementState.Playing)
					music.Stop();
				if (resumeMediaPlayerAfterDone)
					MediaPlayer.Resume();
				if (popup != null)
					popup.IsOpen = false;
				canPlay = false;
				resumeMediaPlayerAfterDone = false;
				gameTimer.Stop(); 
			}
		}

		/// <summary>
		/// Start / Continue music playback. If no file is loaded (or it has reached the end), this will have no effect.
		/// If not SoundSystem.Entered, this has no effect.
		/// </summary>
		public static void PlayMusic()
		{
			lock (threadLock)
			{
				if (canPlay && music.NaturalDuration > new Duration(TimeSpan.FromSeconds(0)))
					music.Play();
			}
		}
		/// <summary>
		/// Play a music file from the beginning.
		/// If not SoundSystem.Entered, this has no effect.
		/// </summary>
		/// <param name="relativePath">relative path to the file</param>
		public static void PlayMusic(string relativePath)
		{
			PlayMusic(relativePath, new TimeSpan(0));
		}
		public static void PlayMusic(Uri source)
		{
			PlayMusic(source, new TimeSpan(0));
		}
		/// <summary>
		/// Play a music file starting at a sepcified offset.
		/// If not SoundSystem.Entered, this has no effect.
		/// </summary>
		/// <param name="relativePath">the poth to the file</param>
		/// <param name="start">the time offset in the file where playback should start</param>
		public static void PlayMusic(string relativePath, TimeSpan start)
		{
			PlayMusic(new Uri(relativePath, UriKind.Relative), start);
		}
		public static void PlayMusic(Uri source, TimeSpan start)
		{
			lock (threadLock)
			{
				if (canPlay && entered)
				{
					string s = source.ToString();
					musicStart = start;
					if (loadedMusicFileUri == s)
					{
						music.Position = start;
						music.Play();
					}
					else
					{
						// in music_MediaOpened, playback is automatically started.
						music.Source = source;
					}
					loadedMusicFileUri = s;
				} 
			}
		}

		/// <summary>
		/// Stops playback and resets MusicPosition to 0:00.
		/// </summary>
		public static void StopMusic()
		{
			lock (threadLock)
			{
				music.Stop();
			}
		}

		/// <summary>
		/// Pauses playback and resmembers the surrent position.
		/// </summary>
		public static void PauseMusic()
		{
			lock (threadLock)
			{
				if (music.CanPause)
					music.Pause();
			}
		}

		/// <summary>
		/// Plays a sound file once using the XNA SoundEffect class and default settings.
		/// Can only play wav files.
		/// </summary>
		/// <param name="relativePath">relative path to the sound file.
		/// Each file is loaded only once and stored for later use.</param>
		public static void PlayEffect(string relativePath)
		{
			try
			{
				LoadEffect(relativePath).Play();
			}
			catch (NullReferenceException)
			{
			}
		}
		/// <summary>
		/// Plays a sound file once using the XNA SoundEffect class and custom settings.
		/// Can only play wav files.
		/// </summary>
		/// <param name="relativePath">relative path to the sound file.
		///  Each file is loaded only once and stored for later use.</param>
		/// <param name="volume">volume (loudness) [0 .. 1]</param>
		/// <param name="pitch">pitch (octaves transposed) [-1 .. 1]</param>
		/// <param name="pan">panorama (left/right position) [-1 .. 1]</param>
		public static void PlayEffect(string relativePath, float volume, float pitch, float pan)
		{
			try
			{
				LoadEffect(relativePath).Play(volume, pitch, pan);
			}
			catch (NullReferenceException)
			{
			}
		}

		/// <summary>
		/// Creates a SoundEffectInstance from a SoundEffect, sets IsLooped to true, and returns it.
		/// 
		/// Note that there can be no more than 16 simultaneous SoundEffectInstances. Every time you 
		/// play a SoundEffect, one is created and disposed when ended. Obviously, looped instances live longer, so
		/// you may get an instance from here, but you are solely responsible for managing it.
		/// </summary>
		/// <param name="sfx">the SoundEffect to loop</param>
		/// <returns>the looping (but stopped) instance.</returns>
		public static SoundEffectInstance CreateLoopingEffectInstance(SoundEffect sfx)
		{
			SoundEffectInstance inst = sfx.CreateInstance();
			inst.IsLooped = true;
			return inst; 
		}
		/// <summary>
		/// Creates a SoundEffectInstance from a SoundEffect, sets IsLooped to true, and returns it.
		/// 
		/// Note that there can be no more than 16 simultaneous SoundEffectInstances. Every time you 
		/// play a SoundEffect, one is created and disposed when ended. Obviously, looped instances live longer, so
		/// you may get an instance from here, but you are solely responsible for managing it.
		/// </summary>
		/// <param name="relativePath">the path to the sound Effect file to loop</param>
		/// <returns>the looping (but stopped) instance.</returns>
		public static SoundEffectInstance CreateLoopingEffectInstance(string relativePath)
		{
			lock (threadLock)
			{
				return CreateLoopingEffectInstance(LoadEffect(relativePath));
			}
		}
		public static SoundEffectInstance CreateLoopingEffectInstance(Uri source)
		{
			lock (threadLock)
			{
				return CreateLoopingEffectInstance(LoadEffect(source));
			}
		}


		/// <summary>
		/// Loads a sound effect file and stores it for reuse.
		/// If it is already loaded, the stored instance is returned.
		/// If the file is not found, a MessageBox is displayed and null is returned.
		/// 
		/// Can only load wav files.
		/// 
		/// This happens automatically if/when PlayEffect() is used.
		/// </summary>
		/// <param name="relativePath">the relative path to the file</param>
		/// <returns>the Xna.Framework.Audio.SoundEffect ready to use, or null.</returns>
		public static SoundEffect LoadEffect(string relativePath)
		{
			while (relativePath.StartsWith("/"))
				relativePath = relativePath.Substring(1);
			return LoadEffect(new Uri(relativePath, UriKind.Relative));
		}
		public static SoundEffect LoadEffect(Uri source)
		{
			string s = source.ToString();
			lock (threadLock)
			{
				if (!effects.ContainsKey(s))
				{
					try
					{
						// Holds informations about a file stream.
						StreamResourceInfo SoundFileInfo = Application.GetResourceStream(source);

						// Create the SoundEffect from the Stream
						SoundEffect se = SoundEffect.FromStream(SoundFileInfo.Stream);
						effects.Add(s, se);
					}
					catch (NullReferenceException)
					{
						// Display an error message
						MessageBox.Show("Couldn't load sound from " + s);
						return null;
					}
				}
				return effects[s];
			}
		}

		/// <summary>
		/// Releases a sound effect from memory.
		/// </summary>
		/// <param name="relativePath">relative path to the file.</param>
		public static void UnloadEffect(string relativePath)
		{
			while (relativePath.StartsWith("/"))
				relativePath = relativePath.Substring(1);
			UnloadEffect(new Uri(relativePath, UriKind.Relative));
		}
		public static void UnloadEffect(Uri source)
		{
			string s = source.ToString();
			lock (threadLock)
			{
				if (effects.ContainsKey(s))
				{
					if (!effects[s].IsDisposed)
						effects[s].Dispose();
					effects.Remove(s);
				} 
			}
		}

		#endregion Methods
	}
}
