using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using UniRx.Triggers;
using UnityEngine.UI;

namespace Osk42 {
	public class Game : MonoBehaviour {

		public GameAssetData assets;

		public Data data;

		class Mover : MonoBehaviour {
			public bool hitGround { get { return 0 < hitList.Count; } }
			public List<GameObject> hitList;
			void Awake() {
				hitList = new List<GameObject>();
			}
		}

		public sealed class ScreenData {
			public static readonly ScreenData instance = new ScreenData();
			public Vector2 design;
			public Vector2 curSize;
			public Vector2 exSize;
			public Rect exRect;
			public float rate;

			public ScreenData() {
				design = new Vector2(640, 480);
				curSize = new Vector2(Screen.width, Screen.height);
				rate = design.y / curSize.y;
				exSize = new Vector2(design.x * rate, design.y);
				exRect = new Rect(0, 0, exSize.x, exSize.y);
			}

			public static void validateSize(ScreenData self) {
				if (self.curSize.x == Screen.width && self.curSize.y == Screen.height) return;
				self.curSize = new Vector2(Screen.width, Screen.height);
				self.rate = self.design.y / self.curSize.y;
				self.exSize = new Vector2(self.design.x * self.rate, self.design.y);
				self.exRect = new Rect(0, 0, self.exSize.x, self.exSize.y);
			}
		}

		public static class Key {
			public static KeyCode Jump = KeyCode.C;
			public static KeyCode Put = KeyCode.Z;
			public static KeyCode Fire = KeyCode.X;
		}

		// Use this for initialization
		void Start() {
			var tr = transform;

			data.canvas = GameObject.FindObjectOfType<Canvas>();
			data.cannon = transform.Find("cannon").gameObject;
			data.progress = new Progress();
			data.player = new Player();
			data.player.gameObject = transform.Find("player").gameObject;
			data.player.gameObject.OnCollisionStayAsObservable().
			Do(_col => {
				if (_col.gameObject.GetComponent<PaperComponent>() != null) return;
				data.player.earth = true;
			}).
			Subscribe().
			AddTo(gameObject);
		}

		public enum StateId {
			Init,
			Ready,
			Main,
			Wait,
			Result1,
			Result2,
		}

		// public sealed class Pool<T> {
		// 	List<T> list_;
		// 	System.Action<T> onAlloc_;
		// 	System.Action<T> onFree_;
		// 	public Pool(System.Func<int, T> creator, System.Action<T> onAlloc, System.Action<T> onFree) {
		// 	}
		// }

		public sealed class PaperComponent : MonoBehaviour {
		}

		public sealed class Paper {
			public static int nextId;
			public ObjectId id;
			public GameObject gameObject;
			public Transform transform;
			public Rigidbody rigidbody;
			public CompositeDisposable disposables = new CompositeDisposable();
		}

		public List<Paper> papers_ = new List<Paper>();

		void FixedUpdate() {
			var player = transform.Find("player");
			var rb = player.GetComponent<Rigidbody>();
			var dir = data.player.dir;
			if (dir != Vector2.zero) {
				var curPos = rb.position;
				if (0f < rb.velocity.y) {
					data.player.earth = false;
				}
				if (data.player.earth && rb.velocity.y <= 0f && 0f < dir.y) {
					var vy = assets.config.jumpPower;
					rb.AddForce(new Vector3(0f, vy, 0f), ForceMode.VelocityChange);
				}
				dir.y = 0f;

				curPos += (Vector3)(dir * assets.config.walkSpeed * Time.fixedDeltaTime);
				curPos.z = 0f;
				rb.position = curPos;
				var rot = rb.rotation;
				var nextRot = rot;
				if (0 < dir.x) {
					nextRot = Quaternion.LookRotation(Vector3.right);
				} else if (dir.x < 0) {
					nextRot = Quaternion.LookRotation(Vector3.left);
				}
				if (rot != nextRot) {
					rb.rotation = nextRot;
				}
			}
		}

		// Update is called once per frame
		void Update() {
			ScreenData.validateSize(ScreenData.instance);
			switch (data.progress.stateId) {
				case StateId.Init:
					data.progress.elapsedTime = 0f;
					data.player.power = 0f;
					data.player.rotScore = 0f;
					data.progress.stateId = StateId.Ready;
					break;
				case StateId.Ready:
					if (!Input.GetKey(KeyCode.Z)) {
						var rot = Quaternion.Euler(
							Random.Range(0f, 360f),
							Random.Range(0f, 360f),
							Random.Range(0f, 360f)
						);
						// var pos = new Vector3(
						// 	Random.Range(-1, 1) * 0.1f,
						// 	Random.Range(0.1f, 1),
						// 	Random.Range(-1, 1) * 0.1f
						// );
						var cannonHead = data.cannon.transform.Find("head_01/head_02");
						var cannonPos = cannonHead.position;
						var cannonRot = cannonHead.rotation;
						var noise = new Vector3(
							Random.Range(-1f, 1f) * 0.2f,
							Random.Range(0f, 1f) * 0.1f,
							Random.Range(-1f, 1f) * 0.2f
						);
						var pos = cannonPos + cannonRot * (Vector3.forward * 1.5f + noise);
						var noisePower = Random.Range(0f, 1f) * 0.5f;
						var power = cannonRot * Vector3.forward * (assets.config.fireForce + noisePower);

						Paper paper;
						if (assets.config.paperCapacity <= papers_.Count) {
							paper = papers_[0];
							paper.gameObject.SetActive(true);
							papers_.RemoveAt(0);
							papers_.Add(paper);
						} else {
							paper = new Paper();
							paper.gameObject = GameObject.Instantiate(assets.paper, pos, rot, transform);
							paper.gameObject.name = string.Format("{0}_{1}", assets.paper.name, papers_.Count);
							paper.rigidbody = paper.gameObject.GetComponent<Rigidbody>();
							paper.transform = paper.gameObject.transform;
							paper.gameObject.AddComponent<PaperComponent>();
							paper.gameObject.OnCollisionEnterAsObservable().
								Do(_col => {
									if (_col.gameObject == data.player.gameObject) {
										paper.gameObject.SetActive(false);
										data.player.score += 1;
									} else {
										paper.gameObject.SetActive(false);
									}
								}).
								Subscribe().
								AddTo(gameObject);
							papers_.Add(paper);
						}
						paper.id = new ObjectId(ObjectType.Paper, Paper.nextId);
						Paper.nextId++;
						paper.transform.position = pos;
						paper.transform.rotation = rot;
						var rb = paper.rigidbody;
						//rb.AddForce(power, ForceMode.VelocityChange);
						rb.AddForceAtPosition(power, cannonPos, ForceMode.VelocityChange);
					}
					if (!Input.GetKey(KeyCode.X)) {
						var cannonHead = data.cannon.transform.Find("head_01");
						var rot = Quaternion.Euler(
							-90f - 45f + Mathf.PingPong(Time.time, 1f) * 90f,
							0f,
							0f);
						cannonHead.localRotation = rot;
					}
					//
					{
						Vector2 dir = new Vector2();

						if (Input.GetKey(KeyCode.LeftArrow)) {
							dir.x -= 1f;
						}
						if (Input.GetKey(KeyCode.RightArrow)) {
							dir.x += 1f;
						}
						if (Input.GetKey(KeyCode.DownArrow)) {
							dir.y -= 1f;
						}
						if (Input.GetKey(KeyCode.UpArrow)) {
							dir.y += 1f;
						}
						data.player.dir = dir;
					}
					if (Input.GetMouseButtonDown(0)) {
						// data.progress.stateId = StateId.Wait;
						// Observable.ReturnUnit().
						// 	Delay(System.TimeSpan.FromSeconds(0.5f)).
						// 	Do(_ => {
						// 		data.progress.stateId = StateId.Main;
						// 	}).
						// 	TakeUntilDestroy(gameObject).
						// 	Subscribe();
					}
					break;
				case StateId.Main: {
						break;
					}
				case StateId.Result1:
					data.progress.stateId = StateId.Wait;
					Observable.ReturnUnit().
						Delay(System.TimeSpan.FromSeconds(0.5f)).
						Do(_ => {
							data.progress.stateId = StateId.Result2;
						}).
						TakeUntilDestroy(gameObject).
						Subscribe();
					break;
				case StateId.Result2:
					if (Input.GetMouseButtonDown(0)) {
						data.progress.stateId = StateId.Init;
					}
					break;
			}


			UpdateView();
		}

		static bool isBlink(float interval) {
			return (Time.time % (interval * 2)) < interval;
		}

		void UpdateView() {
			var progress = data.canvas.transform.Find("progress").GetComponent<Text>();
			var restTime = Mathf.Max(0f, data.progress.timeLimit - data.progress.elapsedTime);
			progress.text =
				string.Format("残り時間 {0:F2} 秒\n", restTime) +
				string.Format("スコア {0:F0} 万円\n", data.player.score) +
				"";
			if (Input.GetKey(KeyCode.LeftShift)) {
				var ipos = Input.mousePosition * ScreenData.instance.rate;
				progress.text += string.Format("pos {0}", ipos);
			}
		}

		[System.Serializable]
		public class Data {
			public GameObject cannon;
			public Canvas canvas;
			public Progress progress;
			public Player player;
			public GameObject stage;
		}

		public class Progress {
			public StateId stateId = StateId.Init;
			public float elapsedTime = 0f;
			public float timeLimit = 30f;

		}

		public class PlayerComponent : MonoBehaviour {
		}

		[System.Serializable]
		public class Player {
			public float power = 0;
			public int score = 0;
			public float rotScore = 0f;
			public Vector2 dir;
			public bool earth;
			public GameObject gameObject;
		}

		public enum ObjectType {
			Player,
			Paper,
		}

		public struct ObjectId : System.IEquatable<ObjectId> {
			public readonly int id;
			public ObjectId(ObjectType type, int id) : this() {
				this.id = ((int)type << 8) | id;
			}
			public bool Equals(ObjectId other) {
				return id == other.id;
			}
			public override bool Equals(object obj) {
				var other = obj as ObjectId?;
				if (other == null) return false;
				return Equals(other);
			}
			public override int GetHashCode() {
				return id;
			}
			public static bool operator ==(ObjectId a, ObjectId b) {
				return a.Equals(b);
			}
			public static bool operator !=(ObjectId a, ObjectId b) {
				return !a.Equals(b);
			}
		}
	}
}
