using UnityEngine;
using uPools;
using VContainer;
using VContainer.Unity;

namespace GameCore.Pooling
{
    public class BaseObjectViewPool<T> : ObjectPoolBase<T> where T : Component, IReturnable<T>
    {
        private readonly IObjectResolver _resolver;
        private readonly T _prefab;
        private readonly Transform _poolParent;
        
        public Transform PoolParent => _poolParent;

        public BaseObjectViewPool(IObjectResolver resolver, T prefab)
        {
            if (prefab == null)
                throw new MissingReferenceException($"{typeof(T).Name} prefab is missing or destroyed.");

            _resolver = resolver;
            _prefab = prefab;

            var gameObject = new GameObject($"{prefab.name}Pool");
            gameObject.SetActive(false);
            _poolParent = gameObject.transform;
        }

        protected override T CreateInstance()
        {
            var instance = _resolver.Instantiate(_prefab);
            instance.ReturnAction = ReturnToPool;
            instance.transform.SetParent(null);
            return instance;
        }

        private void ReturnToPool(T instance)
        {
            if (IsDisposed)
            {
                Object.Destroy(instance.gameObject);
                return;
            }
                
            Return(instance);
        }

        protected override void OnDestroy(T instance)
        {
            if (instance == null)
                return;

            Object.Destroy(instance.gameObject);
        }

        protected override void OnRent(T instance)
        {
            instance.transform.SetParent(null);
        }

        protected override void OnReturn(T instance)
        {
            instance.transform.SetParent(_poolParent);
        }
    }
}
