using System;
namespace ThirdParty {

	/// <summary>
	/// Credit belong to GitAmend 
	/// https://github.com/adammyhre/Unity-Stats-and-Modifiers/blob/master/Assets/_Project/Scripts/Utilities/Timer.cs
	/// </summary>
	public abstract class Timer {
		protected float initialTime;
		public float Time { get; set; }
		public bool IsRunning { get; protected set; }

		public float Progress => initialTime > 0 ? Time / initialTime : 0f;

		public Action OnTimerStart = delegate { };
		public Action OnTimerStop = delegate { };

		protected Timer(float value) {
			initialTime = value;
			IsRunning = false;
		}

		public void Start() {
			Time = initialTime;
			if (!IsRunning) {
				IsRunning = true;
				OnTimerStart?.Invoke();
			}
		}

		public void Stop() {
			if (IsRunning) {
				IsRunning = false;
				OnTimerStop?.Invoke();
			}
		}

		public void Resume() => IsRunning = true;
		public void Pause() => IsRunning = false;

		// Removed the UnityEngine.Time.deltaTime dependency
		// so that Tick method now takes deltaTime as a parameter
		// making it useful out of Unity context
		// since System is a .NET Standard library
		public abstract void Tick(float deltaTime);
	}

	public class CountdownTimer : Timer {
		public CountdownTimer(float coolDownTime) : base(coolDownTime) { }
		public override void Tick(float deltaTime) {
			if (IsRunning && Time > 0)
				Time -= deltaTime;
			if (IsRunning && Time <= 0)
				Stop();
		}
		public bool IsFinished => Time <= 0;

		public void Reset() => Time = initialTime;

		public void Reset(float newTime) {
			initialTime = newTime;
			Reset();
		}

	}

	public class StopwatchTimer : Timer {
		public StopwatchTimer() : base(0) { }

		public override void Tick(float deltaTime) {
			if (IsRunning)
				Time += deltaTime;
		}

		public void Reset() => Time = 0;
		public float GetTime() => Time;

	}


	/// <summary>
	/// My creation, an extension of the base Timer class that allows for interval-based callbacks while counting down.
	/// </summary>
	public class IntervalTimer : Timer {
		private readonly float interval;
		private float timeSinceLastInterval;

		public Action OnInterval = delegate { };

		public IntervalTimer(float duration, float interval) : base(duration) {
			this.interval = interval;
			timeSinceLastInterval = 0f;
		}

		public override void Tick(float deltaTime) {
			if (!this.IsRunning) return;

			this.Time -= deltaTime;
			this.timeSinceLastInterval += deltaTime;

			if (this.timeSinceLastInterval >= this.interval) {
				this.OnInterval?.Invoke();
				this.timeSinceLastInterval = 0f;
			}

			if (this.Time <= 0) Stop();
		}
	}
	/// <summary>
	/// Timer with out the event callbacks, just a simple countdown timer that can be used for things 
	/// like stun duration, etc.
	/// </summary>
	public class BasicCountDownTimer {
		private float Time;
		private readonly float initialTime;
		public bool IsRunning { get; private set; }
		public BasicCountDownTimer(float time) {
			this.initialTime = time;
			this.Time = time;
		}
		public void Tick(float deltaTime) {
			if (!this.IsRunning) return;
			Time -= deltaTime;
			if (Time <= 0) {
				Time = 0;
				IsRunning = false;
			}
		}
		public void Start() {
			Time = initialTime;
			IsRunning = true;
		}
		public void Reset(float newTime) {
			this.Time = newTime;
		}
		public void Stop() => IsRunning = false;


	}
}