using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Oxide.Core;
using Rust;

namespace Oxide.Plugins {
	
	[Info("Bus Stop Teleport", "yetzt", "1.0.0")]
	[Description("Teleport using Bus Stops")]

	public class BusStopTeleport : RustPlugin {

		[PluginReference]
		private Plugin NoEscape;

		List<ulong> hasUI = new List<ulong>();
		private readonly string Layer = "busstopUI";

		class StoredData {
			public Dictionary<NetworkableId, BusStop> BusStops = new Dictionary<NetworkableId, BusStop>();
			public List<NetworkableId> Chairs = new List<NetworkableId>();
			public StoredData(){}
		}
		StoredData storedData;

		class BusStop {
			public Vector3 Location = new Vector3();
			public string Label = "";
			public string Grid = "";
			public NetworkableId Chair = new NetworkableId(0);
			public BusStop(){}
			public BusStop(Vector3 pos, string label, string grid, NetworkableId chair) {
				Location = pos;
				Label = label;
				Grid = grid;
				Chair = chair;
			}
		}

		BusStop busStop;

		void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("BusStopTeleport", storedData);

		private void Init() {
			cmd.AddConsoleCommand("busstop.close", this, nameof(closeUI));
			cmd.AddConsoleCommand("busstop.teleport", this, nameof(cmdTeleport));
			cmd.AddConsoleCommand("busstop.reset", this, nameof(cmdResetNetwork));
		}
		
		private bool IsAdmin(BasePlayer player) {
			if (player == null) return false;
			if (player?.net?.connection == null) return true;
			return player.net.connection.authLevel > 0;
		}
		
		
		[ChatCommand("bus")]
		void replyBus(BasePlayer player, string cmd, string[] args) {
			if (IsAdmin(player)) showUI(player, new NetworkableId(0));
		}
		
		void OnServerInitialized() {
			Puts("Initializing");
			storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("BusStopTeleport");
			resetNetwork();
		}
		
		void Unload() {

			// Remove Chairs
			foreach(var item in storedData.BusStops.Values) {
				var chair = BaseNetworkable.serverEntities.Find(item.Chair) as BaseEntity;
				if (chair != null) chair.Kill();
			}
			
			// Delete Data
			storedData.Clear();
			
		};

		void showUI(BasePlayer player, NetworkableId chair) {

			CuiHelper.DestroyUi(player, Layer);
			if (hasUI.Contains(player.userID)) {
				hasUI.Remove(player.userID);
				return;
			} 
			
			var container = new CuiElementContainer();

			// overlay
			container.Add(new CuiPanel {
				CursorEnabled = true,
				Image = { 
					Color = "0 0 0 0" 
				},
				RectTransform = { 
					AnchorMin = "0 0", 
					AnchorMax = "1 1" 
				}
			}, "Overlay", Layer);

			// background
			container.Add(new CuiPanel {
				Image = new CuiImageComponent
				{
					Color = "0 0 0 0.95"
				},
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "0.4 1"
				}
			}, Layer);

			// close
			container.Add(new CuiButton {
				RectTransform = {
					AnchorMin = "0.02 0.1",
					AnchorMax = "0.12 0.16"
				},
				Button = {
					Command = "busstop.close",
					Color = "0.4 0.15 0.1 0.9",
					Close = Layer
				},
				Text = {
					Align = TextAnchor.MiddleCenter,
					Text = "EXIT",
					Color = "0.9 0.9 0.9 0.9",
					FontSize = 20
				}
			}, Layer);
			
			// headline
			container.Add(new CuiLabel {
				RectTransform = {
					AnchorMin = "0.02 0.90",
					AnchorMax = "0.2 0.97"
				},
				Text = {
					Text = "RUST",
					Align = TextAnchor.MiddleLeft,
					Color = "1 1 1 1",
					FontSize = 50
				}
			}, Layer);

			container.Add(new CuiLabel {
				RectTransform = {
					AnchorMin = "0.02 0.84",
					AnchorMax = "0.2 0.91"
				},
				Text = {
					Text = "TRANSPORT",
					Align = TextAnchor.MiddleLeft,
					Color = "1 1 1 1",
					FontSize = 50
				}
			}, Layer);

			container.Add(new CuiLabel {
				RectTransform = {
					AnchorMin = "0.02 0.78",
					AnchorMax = "0.2 0.85"
				},
				Text = {
					Text = "NETWORK",
					Align = TextAnchor.MiddleLeft,
					Color = "1 1 1 1",
					FontSize = 50
				}
			}, Layer);
			
			// add vertical line
			container.Add(new CuiPanel {
				Image = new CuiImageComponent
				{
					Color = "0 0.7 1 1"
				},
				RectTransform =
				{
					AnchorMin = "0.248 0",
					AnchorMax = "0.252 1"
				}
			}, Layer);
			
			int i = 0;
			foreach(var item in storedData.BusStops.Values) {
			
				if (chair == item.Chair) continue;
			
				//a dot
				container.Add(new CuiPanel {
					Image = new CuiImageComponent {
						Color = "0 0.7 1 1"
					},
					RectTransform = {
						AnchorMin = "0.246 "+(0.94f-(i*0.035f)).ToString(),
						AnchorMax = "0.254 "+(0.953f-(i*0.035f)).ToString()
					}
				}, Layer);

				container.Add(new CuiLabel {
					RectTransform = {
						AnchorMin = "0.22 "+(0.93f-(i*0.035f)).ToString(),
						AnchorMax = "0.24 "+(0.963f-(i*0.035f)).ToString()
					},
					Text = {
						Text = item.Grid,
						Align = TextAnchor.MiddleRight,
						Color = "0.7 0.7 0.7 1",
						FontSize = 20
					}
				}, Layer);
			
				container.Add(new CuiButton {
					RectTransform = {
						AnchorMin = "0.26 "+(0.93f-(i*0.035f)).ToString(),
						AnchorMax = "0.4 "+(0.963f-(i*0.035f)).ToString()
					},
					Button = {
						Command = "busstop.teleport "+item.Chair,
						Color = "0 0 0 0",
						Close = Layer
					},
					Text = {
						Align = TextAnchor.MiddleLeft,
						Text = item.Label.ToUpper(),
						Color = "1 1 1 1",
						FontSize = 20
					}
				}, Layer);
				
				i++;
			
			}
			
			CuiHelper.DestroyUi(player, Layer);
			CuiHelper.AddUi(player, container);
			
			hasUI.Add(player.userID);

		}

		private void resetNetwork() {
			
			foreach(var item in storedData.BusStops.Values) {
				var chair = BaseNetworkable.serverEntities.Find(item.Chair) as BaseEntity;
				if (chair != null) chair.Kill();
			}

			storedData.BusStops = new Dictionary<NetworkableId, BusStop>();
			storedData.Chairs = new List<NetworkableId>();

			FindMonuments();
			
		}
		
		private void closeUI(ConsoleSystem.Arg arg) {
			var player = arg.Player();
			if (player == null) return;
			player.EnsureDismounted();
			CuiHelper.DestroyUi(player, Layer);
			hasUI.Remove(player.userID);
		}
		
		private void cmdTeleport(ConsoleSystem.Arg arg) {
			var player = arg.Player();
			if (player == null) return;
			if (!hasUI.Contains(player.userID)) return;
			CuiHelper.DestroyUi(player, Layer);
			hasUI.Remove(player.userID);

			BusStop busstop;
			if (!storedData.BusStops.TryGetValue(new NetworkableId(Convert.ToUInt32(arg.Args[0])), out busstop)) return;

			performTeleport(player, busstop.Location);
			return;
		}
		
		private void cmdResetNetwork(ConsoleSystem.Arg arg) {
			if (IsAdmin(arg.Player())) resetNetwork();
		}

		void performTeleport(BasePlayer player, Vector3 position){
						
			HeldEntity heldEntity = player.GetHeldEntity();
			if (heldEntity != null) heldEntity.SetHeld(false);

			player.EnsureDismounted();
			
			if (player.net?.connection != null) player.ClientRPCPlayer(null, player, "StartLoading");
			
			if (!player.IsSleeping()) {
				player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
				if (!BasePlayer.sleepingPlayerList.Contains(player)) BasePlayer.sleepingPlayerList.Add(player);
				player.CancelInvoke("InventoryUpdate");
			}

			player.MovePosition(position);
			if (player.net?.connection != null) player.ClientRPCPlayer(null, player, "ForcePositionTo", position);

			if (player.net?.connection != null) player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);

			player.UpdateNetworkGroup();
			player.SendNetworkUpdateImmediate();

			if (player.net?.connection == null) return;
			try { player.ClearEntityQueue(); } catch { }

			player.SendFullSnapshot();
			player.SetParent(null, true, true);

			Wakeup(player);

			return;
			
		}
		
		private void Wakeup(BasePlayer player) {
			if (!player.IsConnected) return;
			if (player.IsReceivingSnapshot) {
				timer.Once(1f, () => Wakeup(player));
				return;
			}
			player.EndSleeping();
		}

		void FindMonuments() {

			Dictionary<NetworkableId, GameObject> busstops = new Dictionary<NetworkableId, GameObject>();

			foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>()) {
				if (go.name != "assets/bundled/prefabs/autospawn/decor/busstop/busstop.prefab") continue;
				
				// spawn invisible chair, fallback to visible chair
				var chair = GameManager.server.CreateEntity("assets/bundled/prefabs/static/chair.invisible.static.prefab", go.transform.position, go.transform.rotation, true);
				if (chair == null) chair = GameManager.server.CreateEntity("assets/bundled/prefabs/static/chair_b.static.prefab", go.transform.position, go.transform.rotation, true);

				chair.transform.RotateAround(go.transform.position, Vector3.up, 90f);
				chair.transform.Translate(Vector3.up * 0.52f);
				chair.transform.Translate(Vector3.forward * -1.7f);

				chair.Spawn();

				Vector3 pos = go.transform.position;
				pos.y = pos.y+0.3f;
				
				storedData.Chairs.Add(chair.net.ID);
				busstops.Add(chair.net.ID, go);

			}
			
			Puts($"Found {busstops.Count} Bus Stops.");

			List<NetworkableId> doublecheck = new List<NetworkableId>();

			foreach (var monument in TerrainMeta.Path.Monuments) {
				if (monument.name.Contains("substation") || monument.name.Contains("cave") || monument.name.Contains("tiny")) continue;
				
				float size = 0f;
				string name = "";

				switch (monument.name.Split('/').Last()) {
					case "lighthouse.prefab":
						name = "Lighthouse";
						size = 100f;
						break;
						
					case "stables_a.prefab":
						name = "Stables";
						size = 100f;
						break;
						
					case "stables_b.prefab":
						name = "Barn";
						size = 100f;
						break;
						
					case "fishing_village_a.prefab":
					case "fishing_village_b.prefab":
					case "fishing_village_c.prefab":
						name = "Fishing Village";
						size = 100f;
						break;
						
					case "harbor_1.prefab":
					case "harbor_2.prefab":
						name = "Harbor";
						size = 150f;
						break;

					case "excavator_1.prefab":
						name = "Excavator";
						size = 300f;
						break;

					case "launch_site_1.prefab":
						name = "Launch Site";
						size = 400f;
						break;

					case "trainyard_1.prefab":
						name = "Trainyard";
						size = 200f;
						break;

					case "water_treatment_plant_1.prefab":
						name = "Water Treatment Plant";
						size = 200f;
						break;

					case "military_tunnel_1.prefab":
						name = "Military Tunnel";
						size = 200f;
						break;

					case "powerplant_1.prefab":
						name = "Powerplant";
						size = 200f;
						break;

					case "airfield_1.prefab":
						name = "Airfield";
						size = 200f;
						break;

					case "compound.prefab":
						name = "Outpost";
						size = 150f;
						break;

					case "bandit_town.prefab":
						name = "Bandit Camp";
						size = 150f;
						break;

					case "radtown_small_3.prefab":
						name = "Sewer Branch";
						size = 200f;
						break;

					case "warehouse.prefab":
						name = "Mining Outpost";
						size = 100f;
						break;

					case "satellite_dish.prefab":
						name = "Satellite Dish";
						size = 150f;
						break;

					case "supermarket_1.prefab":
						name = "Supermarket";
						size = 100f;
						break;

					case "gas_station_1.prefab":
						name = "Gas Station";
						size = 100f;
						break;

					case "junkyard_1.prefab":
						name = "Junkyard";
						size = 200f;
						break;

					case "swamp_c.prefab": 
						name = "Abandonned Cabins";
						size = 150f;
						break;

					case "sphere_tank.prefab": 
						name = "Dome";
						size = 150f;
						break;

					case "mining_quarry_a.prefab": 
						name = "Sulphur Quarry";
						size = 100f;
						break;

					case "mining_quarry_b.prefab": 
						name = "Stone Quarry";
						size = 100f;
						break;

					case "mining_quarry_c.prefab": 
						name = "HQM Quarry";
						size = 100f;
						break;

					case "OilrigAI":
						name = "Small Oilrig";
						size = 100f;
						break;

					case "OilrigAI2":
						name = "Large Oilrig";
						size = 150f;
						break;

					case "desert_military_base_a.prefab":
					case "desert_military_base_b.prefab":
					case "desert_military_base_c.prefab":
					case "desert_military_base_d.prefab":
						name = "Desert Military Base";
						size = 150f;
						break;

					case "arctic_research_base_a.prefab":
						name = "Arctic Research Base";
						size = 150f;
						break;

					case "monument_marker.prefab":
						// Puts("marker "+monument.name+" "+monument.transform.root.name);
						name = monument.transform.root.name;
						size = 100f;
						break;
					case "cave_large_hard.prefab":
					case "cave_large_medium.prefab":
					case "cave_medium_easy.prefab":
					case "cave_medium_hard.prefab":
					case "cave_medium_medium.prefab":
					case "cave_small_easy.prefab":
					case "cave_small_hard.prefab":
					case "cave_small_medium.prefab":
					case "ice_lake_1.prefab":
					case "ice_lake_2.prefab":
					case "ice_lake_3.prefab":
					case "ice_lake_4.prefab":
					case "swamp_a.prefab":
					case "swamp_b.prefab":
					case "water_well_a.prefab":
					case "water_well_b.prefab":
					case "water_well_c.prefab":
					case "water_well_d.prefab":
					case "water_well_e.prefab":
					case "underwater_lab_a.prefab":
					case "underwater_lab_b.prefab":
					case "underwater_lab_c.prefab":
					case "underwater_lab_d.prefab":
					case "entrance_bunker_a.prefab":
					case "entrance_bunker_b.prefab":
					case "entrance_bunker_c.prefab":
					case "entrance_bunker_d.prefab":
						// ignore
						continue;
					break;
					default:
						Puts($"Unknown Monument: "+monument.name.Split('/').Last());
						continue;
				}

				// find closest bus stop
				
				float dist = 1000f;
				float newdist = 0f;
				bool found = false;
				GameObject closest = new GameObject();
				NetworkableId closestchair = new NetworkableId(0);

				foreach (KeyValuePair<NetworkableId, GameObject> busstop in busstops) {

					if (doublecheck.Contains(busstop.Key)) continue;
				
					newdist = Vector3.Distance(monument.transform.position, busstop.Value.transform.position);
					// Puts($"{monument.transform.position} ↔ {name}: {newdist}");
					if (newdist > size) continue;
					if (newdist < dist) {
						found = true;
						dist = newdist;
						closest = busstop.Value;
						closestchair = busstop.Key;
					}
					
				}
				
				if (found) {
					Puts($"Found Busstop for '{name}' at {closest.transform.position}");
				
					// add bus stop to dboubleckeck
					doublecheck.Add(closestchair);
				
					Vector3 pos = closest.transform.position;
					pos.y = pos.y+0.3f;
					
					string grid = getGrid(pos);
					busStop = new BusStop(pos, name, grid, closestchair);
				
					storedData.BusStops.Add(closestchair, busStop);
					
				} else {
					Puts($"No Busstop for '{name}'");
				}
				
			}

			// return list
			SaveData();
			
		}

		string getGrid(Vector3 pos) { // FIXME this is not always correct
			char letter = 'A';
			var x = Mathf.Floor((pos.x+(ConVar.Server.worldsize/2)) / 146.3f)%26;
			var z = (Mathf.Floor(ConVar.Server.worldsize/146.3f))-Mathf.Floor((pos.z+(ConVar.Server.worldsize/2)) / 146.3f);
			letter = (char)(((int)letter)+x);
			return $"{letter}{z}";
		}
		
		object CanMountEntity(BasePlayer player, BaseMountable entity) {

			if (!storedData.Chairs.Contains(entity.net.ID)) return null;
			
			// noescape integration
			var flag = NoEscape?.Call<bool>("IsEscapeBlocked", player) ?? false;
			if (flag == true) {
				Player.Message(player, "You are still blocked from taking the bus");
				return null;
			}
			
			showUI(player, entity.net.ID);
			return null;

		}

		object CanDismountEntity(BasePlayer player, BaseMountable entity) {

			if (player == null) return null;

			if (!storedData.Chairs.Contains(entity.net.ID)) return null;
			
			CuiHelper.DestroyUi(player, Layer);
			hasUI.Remove(player.userID);

			return null;

		}
	
	}

}