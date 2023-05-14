using System;
using System.Collections.Generic;
using UnityEngine;

namespace Resight.Utilities
{
    public class MainQueue : MonoBehaviour
    {
        private static readonly Queue<Action> _actionsPushed = new Queue<Action>();
        private readonly Queue<Action> _actionsPulled = new Queue<Action>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnLoad()
        {
            var _hiddenMainQueue = new GameObject("_MainQueue");
            DontDestroyOnLoad(_hiddenMainQueue);
            _hiddenMainQueue.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector;
            _hiddenMainQueue.AddComponent<MainQueue>();
        }

        public static void Reset()
        {
            lock (_actionsPushed) {
                _actionsPushed.Clear();
            }
        }

        public static void Enqueue(Action action)
        {
            lock (_actionsPushed) {
                _actionsPushed.Enqueue(action);
            }
        }

        private void Update()
        {
            // copy the queued actions to a local array, to minimize lock retention
            lock (_actionsPushed) {
                while (_actionsPushed.Count > 0) {
                    _actionsPulled.Enqueue(_actionsPushed.Dequeue());
                }
            }

            // invoke actions on the local queue
            foreach (var action in _actionsPulled) {
                action?.Invoke();
            }

            _actionsPulled.Clear();
        }
    }
}
