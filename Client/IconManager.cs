using System;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable Unity.InefficientPropertyAccess

namespace HkmpTag.Client {
    /// <summary>
    /// Manager class for the icon that shows above players that have won the game.
    /// </summary>
    public class IconManager {
        /// <summary>
        /// The GameObject for the crown prefab.
        /// </summary>
        private GameObject _crownPrefab;

        /// <summary>
        /// The GameObject for the current crown object.
        /// </summary>
        private GameObject _currentCrownObject;

        /// <summary>
        /// Initialize the manager by creating the crown object.
        /// </summary>
        public void Initialize() {
            CreateCrownObject();
        }

        /// <summary>
        /// Shows the crown on the given player container.
        /// </summary>
        /// <param name="playerContainer">The GameObject for the player container.</param>
        public void ShowOnPlayer(GameObject playerContainer) {
            if (_currentCrownObject == null) {
                _currentCrownObject = Object.Instantiate(_crownPrefab);
            }

            _currentCrownObject.GetComponent<IconBehaviour>().Host = playerContainer;
            _currentCrownObject.SetActive(true);
        }

        /// <summary>
        /// Hides the crown object.
        /// </summary>
        public void Hide() {
            if (_currentCrownObject != null) {
                _currentCrownObject.SetActive(false);
            }
        }

        /// <summary>
        /// Creates the crown prefab.
        /// </summary>
        private void CreateCrownObject() {
            var sprite = LoadIconSprite();

            _crownPrefab = new GameObject();
            var spriteRenderer = _crownPrefab.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;

            _crownPrefab.AddComponent<IconBehaviour>();
            _crownPrefab.SetActive(false);
            Object.DontDestroyOnLoad(_crownPrefab);
        }

        /// <summary>
        /// Load the sprite for the crown icon.
        /// </summary>
        /// <returns>A Sprite instance.</returns>
        private Sprite LoadIconSprite() {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var resourceStream =
                executingAssembly.GetManifestResourceStream("HKMPTag.Client.Resource.crown.png");
            if (resourceStream == null) {
                return null;
            }

            var dataBuffer = new byte[resourceStream.Length];
            resourceStream.Read(dataBuffer, 0, dataBuffer.Length);
            resourceStream.Dispose();

            var iconTex = new Texture2D(1, 1);
            iconTex.LoadImage(dataBuffer);

            var sprite = Sprite.Create(
                iconTex,
                new Rect(0f, 0f, iconTex.width, iconTex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );

            return sprite;
        }
    }

    /// <summary>
    /// MonoBehaviour for the crown icon.
    /// </summary>
    public class IconBehaviour : MonoBehaviour {
        /// <summary>
        /// The Y-offset of the crown relative to the player.
        /// </summary>
        private const float YOffset = 2.2f;

        /// <summary>
        /// The Y amplitude of the crown movement.
        /// </summary>
        private const float YAmplitude = 0.25f;

        /// <summary>
        /// The speed of the crown movement.
        /// </summary>
        private const float Speed = 1f;

        /// <summary>
        /// Time of the last cycle.
        /// </summary>
        private float _lastCycle;

        /// <summary>
        /// The GameObject on which the crown object is placed.
        /// </summary>
        public GameObject Host { set; private get; }

        public void Start() {
            SetLocation();
        }

        public void OnEnable() {
            SetLocation();
        }

        /// <summary>
        /// Set the location of the crown object on the host.
        /// </summary>
        private void SetLocation() {
            if (Host == null) {
                return;
            }

            transform.localScale = new Vector2(0.2f, 0.2f);
            transform.SetParent(Host.transform);
        }

        public void Update() {
            _lastCycle = (_lastCycle + Speed * Time.deltaTime) % 1f;

            transform.localPosition = new Vector3(
                0f,
                YOffset + YAmplitude * (float)Math.Sin(_lastCycle * 2f * Math.PI)
            );
        }
    }
}
