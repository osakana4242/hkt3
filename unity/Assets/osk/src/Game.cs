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
			public static KeyCode Jump = KeyCode.Z;
			public static KeyCode Ok = KeyCode.Z;
		}

		// Use this for initialization
		void Start() {
			var tr = transform;

			data.canvas = GameObject.FindObjectOfType<Canvas>();
			data.progress = new Progress();
			data.cannon = new Cannon();
			data.cannon.pause = true;
			data.cannon.gameObject = transform.Find("cannon").gameObject;
			data.player = new Player();
			data.player.gameObject = transform.Find("player").gameObject;
			data.player.animator = data.player.gameObject.GetComponentInChildren<Animator>();
			data.player.startPosition = data.player.gameObject.transform.localPosition;
			data.textEventList = new List<TextEvent>();

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

		public class TextEvent {
			public string text;
			public float restTime;
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

		void updatePlayer() {
			var player = data.player;

			{ // chain
				if (0f < player.chainTime) {
					player.chainTime -= Time.fixedDeltaTime;
					if (player.chainTime < 0f) {
						player.chainTime = 0f;
						player.chainCount = 0;
					}
				}
			}

			var playerTr = player.gameObject.transform;
			var rb = playerTr.GetComponent<Rigidbody>();
			var dir = player.dir;
			dir.y = 0f;
			var curPos = rb.position;

			if (0.1f < rb.velocity.y) {
				player.earth = false;
			}

			if (player.hasJump) {
				if (player.earth) {
					player.jumpCount = 0;
				}
				if (player.jumpCount < assets.config.jumpCount) {
					player.jumpCount++;
					player.hasJump = false;
					var vy = assets.config.jumpPower;
					//rb.AddForce(new Vector3(dir.x * assets.config.walkSpeed, vy, 0f), ForceMode.VelocityChange);
					rb.velocity = Vector3.zero;
					rb.AddForce(new Vector3(0f, vy, 0f), ForceMode.VelocityChange);
				}
			}

			if (dir != Vector2.zero) {
				//if (player.earth) {
				{
					curPos += (Vector3)(dir * assets.config.walkSpeed * Time.fixedDeltaTime);
					curPos.z = 0f;
					rb.position = curPos;
				}
				{
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

			{
				var pos = (Vector2)playerTr.localPosition;
				if (!assets.config.activeRect.Contains(pos)) {
					player.isDead = true;
				}
			}
			{
				var nextAnim = player.anim;
				nextAnim = "idle";
				if (dir != Vector2.zero || !player.earth) {
					nextAnim = "run";
				}
				if (player.anim != nextAnim) {
					player.anim = nextAnim;
					player.animator.CrossFadeInFixedTime(nextAnim, 0.1f, 0, 0f);
				}
			}

		}

		void updateCannon() {
			if (data.cannon.pause) return;

			{
				data.cannon.elapsedTime += (int)(Time.fixedDeltaTime * 1000f);
				var cannonHead = data.cannon.gameObject.transform.Find("head_01");
				float x = 0f;

				{
					var center = -90f;
					var range = 60f;
					x = center - (range / 2) + Mathf.PingPong(data.cannon.elapsedTime / (10f * 1000f), 1f) * range;
				}
				{
					var center = x;
					var range = 120f;
					x = center - (range / 2) + Mathf.PingPong(data.cannon.elapsedTime / (1f * 1000f), 1f) * range;
				}
				// {
				// 	var center = x;
				// 	var range = 15f;
				// 	x = center - (range / 2) + Mathf.PingPong(data.cannon.elapsedTime * 0.25f / 1000f, 1f) * range;
				// }


				var rot = Quaternion.Euler(x, 0f, 0f);
				cannonHead.localRotation = rot;
			}
		}

		string[] greatList_ = {
			"",
			"いいね",
			"いいね",
			"すごいね",
			"やったね",
			"わーい",
			"わーーい",
			"超いいね",
		};

		void updatePaper() {
			if (data.cannon.pause) return;
			if (data.progress.elapsedTime < 1f) return;
			var restTIme = data.progress.timeLimit - data.progress.elapsedTime;
			if (restTIme < 3f) return;

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
			var cannonHead = data.cannon.gameObject.transform.Find("head_01/head_02");
			var cannonPos = cannonHead.position;
			cannonPos.z = 0f;
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
				paper.rigidbody.velocity = Vector3.zero;
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
							if (true) {
								if (data.player.chainTime <= 0f) {
									data.player.chainTime = assets.config.chainTime;
									data.player.chainCount = 0;
								} else {
									data.player.chainCount = Mathf.Min(data.player.chainCount + 1, assets.config.chainMax);
								}
								var score = 100 * (int)Mathf.Pow(2, data.player.chainCount);
								data.player.score += score;
								data.textEventList.Add(new TextEvent() {
									text = string.Format("+{0} {1}", score, greatList_[Mathf.Clamp(data.player.chainCount, 0, greatList_.Length - 1)]),
									restTime = 0.5f,
								});
							} else {
								if (data.player.earth) {
									var score = 100;
									data.player.score += score;
									data.textEventList.Add(new TextEvent() {
										text = string.Format("+{0}", score),
										restTime = 0.5f,
									});
								} else {
									var score = 100 * (int)Mathf.Pow(2, data.player.jumpCount);
									data.player.score += score;
									switch (data.player.jumpCount) {
										case 1:
											data.textEventList.Add(new TextEvent() {
												text = string.Format("+{0} ジャンプボーナス", score),
												restTime = 0.5f,
											});
											break;
										case 2:
											data.textEventList.Add(new TextEvent() {
												text = string.Format("+{0} 2段ジャンプボーナス", score),
												restTime = 0.5f,
											});
											break;
									}
								}
							}
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

		void FixedUpdate() {
			updatePlayer();
			updateCannon();
			updatePaper();
		}

		// Update is called once per frame
		void Update() {
			ScreenData.validateSize(ScreenData.instance);
			switch (data.progress.stateId) {
				case StateId.Init: {
						data.progress.elapsedTime = 0f;
						data.cannon.elapsedTime = 0;
						data.cannon.pause = true;
						data.player.power = 0f;
						data.player.score = 0;
						var rb = data.player.gameObject.GetComponent<Rigidbody>();
						rb.velocity = Vector3.zero;
						data.player.gameObject.transform.localPosition = data.player.startPosition;
						data.progress.stateId = StateId.Ready;
						data.player.isDead = false;

						for (int i = 0; i < papers_.Count; i++) {
							var paper = papers_[i];
							paper.gameObject.SetActive(false);
							paper.rigidbody.velocity = Vector3.zero;
						}

						break;
					}
				case StateId.Ready: {
						if (Input.GetKey(Key.Ok)) {
							data.cannon.pause = false;
							data.progress.stateId = StateId.Main;
						}
						break;
					}
				case StateId.Main:
					//
					{
						if (Input.GetKeyDown(Key.Jump)) {
							data.player.hasJump = true;
						}
					} {
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
					if (!data.progress.pause) {
						data.progress.elapsedTime += Time.deltaTime;
					}
					if (data.progress.timeLimit <= data.progress.elapsedTime || data.player.isDead) {
						data.progress.elapsedTime = data.progress.timeLimit;
						data.progress.stateId = StateId.Result1;
					}
					break;
				case StateId.Result1:
					data.cannon.pause = true;
					data.player.dir = Vector2.zero;
					data.progress.stateId = StateId.Wait;
					Observable.ReturnUnit().
						Delay(System.TimeSpan.FromSeconds(2f)).
						Do(_ => {
							data.progress.stateId = StateId.Result2;
						}).
						TakeUntilDestroy(gameObject).
						Subscribe();
					break;
				case StateId.Result2:
					if (Input.GetKey(Key.Ok)) {
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
			{
				var evtText = data.canvas.transform.Find("event").GetComponent<Text>();
				var sb = new System.Text.StringBuilder();
				{
					var list = data.textEventList;
					for (var i = 0; i < list.Count; i++) {
						var evt = list[i];
						evt.restTime -= Time.deltaTime;
					}
					for (var i = list.Count - 1; 0 <= i; i--) {
						var evt = list[i];
						if (0f < evt.restTime) continue;
						list.RemoveAt(i);
					}
					for (var i = list.Count - 1; Mathf.Max(0, list.Count - 4) <= i; i--) {
						var evt = list[i];
						int length = (int)Mathf.Lerp(0, evt.text.Length, Mathf.Min(evt.restTime, 0.1f) * 10f);
						sb.AppendFormat("{0}\n", evt.text.Substring(0, length));
					}
				}
				var text = sb.ToString();
				if (evtText.text != text) {
					evtText.text = text;
				}
			}

			{
				var progress = data.canvas.transform.Find("progress").GetComponent<Text>();
				var restTime = Mathf.Max(0f, data.progress.timeLimit - data.progress.elapsedTime);
				var restTimeText = (0 < restTime) ?
					string.Format("残り時間 {0:F2} 秒", restTime) :
					"終了";

				progress.text =
					string.Format("{0}\n", restTimeText) +
					string.Format("スコア {0:F0}\n", data.player.score) +
					"";
				if (Input.GetKey(KeyCode.LeftShift)) {
					var ipos = Input.mousePosition * ScreenData.instance.rate;
					progress.text += string.Format("pos {0}", ipos);
				}
			}
		}

		[System.Serializable]
		public class Data {
			public Cannon cannon;
			public Canvas canvas;
			public Progress progress;
			public Player player;
			public GameObject stage;
			public List<TextEvent> textEventList;
		}

		[System.Serializable]
		public class Progress {
			public StateId stateId = StateId.Init;
			public float elapsedTime = 0f;
			public float timeLimit = 30f;
			public bool pause = false;

		}

		public class PlayerComponent : MonoBehaviour {
		}

		[System.Serializable]
		public class Player {
			public float power = 0;
			public int score = 0;
			public int chainCount = 0;
			public float chainTime = 0;
			public Vector2 dir;
			public bool hasJump;
			public bool earth;
			public int jumpCount;
			public GameObject gameObject;
			public Animator animator;
			public Vector3 startPosition;
			public bool isDead;
			public string anim;
		}

		[System.Serializable]
		public class Cannon {
			public bool pause;
			public GameObject gameObject;
			public int elapsedTime;
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
