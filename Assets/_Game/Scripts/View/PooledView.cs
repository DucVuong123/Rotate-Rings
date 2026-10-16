using System;
using System.Threading;
using GameCore.Pooling;
using UnityEngine;

namespace RotateRings.Gameplay.View
{
    /// <summary>
    /// Base for views rented from BaseObjectViewPool. uPools does not call OnRent for freshly created
    /// instances, so each rent must start with BeginUse() instead of relying on pool callbacks.
    /// </summary>
    public abstract class PooledView<T> : MonoBehaviour, IReturnable<T> where T : PooledView<T>
    {
        CancellationTokenSource _useCts;

        public Action<T> ReturnAction { get; set; }

        /// <summary>Cancelled when the view goes back to the pool or is destroyed.</summary>
        protected CancellationToken UseToken => _useCts?.Token ?? destroyCancellationToken;

        protected void BeginUse()
        {
            CancelUse();
            _useCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            gameObject.SetActive(true);
        }

        public void ReturnToPool()
        {
            CancelUse();
            OnReturning();

            if (ReturnAction != null)
                ReturnAction((T)this);
            else
                Destroy(gameObject);
        }

        protected virtual void OnReturning()
        {
        }

        void CancelUse()
        {
            if (_useCts == null)
                return;

            _useCts.Cancel();
            _useCts.Dispose();
            _useCts = null;
        }

        protected virtual void OnDestroy() => CancelUse();
    }
}
