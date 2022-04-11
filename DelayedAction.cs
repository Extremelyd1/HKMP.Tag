using System;
using System.Timers;

namespace HkmpTag {
    /// <summary>
    /// Class to delay executing a given action by a given time. 
    /// </summary>
    public class DelayedAction {
        /// <summary>
        /// The time to wait before executing.
        /// </summary>
        public double Time { get; set; }

        /// <summary>
        /// The action to execute.
        /// </summary>
        public Action Action { get; set; }

        /// <summary>
        /// The timer that is used to schedule the execution.
        /// </summary>
        private Timer _timer;

        public DelayedAction(double time, Action action) {
            Time = time;
            Action = action;
        }

        /// <summary>
        /// Start the timer to execute the action after the given delay.
        /// </summary>
        public void Start() {
            _timer?.Dispose();

            _timer = new Timer(Time);
            _timer.Elapsed += (sender, args) => {
                Action.Invoke();

                Stop();
            };
            _timer.Start();
        }

        /// <summary>
        /// Stops the timer if it was started.
        /// </summary>
        public void Stop() {
            if (_timer != null) {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }
    }
}
