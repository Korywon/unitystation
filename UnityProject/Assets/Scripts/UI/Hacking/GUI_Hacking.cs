﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class GUI_Hacking : NetTab
{
	private IHackable hackInterface;
	private HackingProcessBase hackProcess;
	public HackingProcessBase HackProcess => hackProcess;

	/// <summary>
	/// This is a list of all the hacking nodes for this object.
	/// </summary>
	private List<HackingNode> hackNodes;
	public List<HackingNode> HackNodes => hackNodes;

	/// <summary>
	/// Input and output node lists, comprised of nodes from the list of all hacking nodes.
	/// </summary>
	private List<HackingNode> inputNodes;
	public List<HackingNode> InputNodes => inputNodes;

	private List<HackingNode> outputNodes;
	public List<HackingNode> OutputNodes => outputNodes;

	/// <summary>
	/// The list of node UI objects the panel has created. This will be used to delete/modify existing nodes if they get updated/replaced.
	/// </summary>
	private List<GUI_HackingNode> nodeUIObjects = new List<GUI_HackingNode>();
	public List<GUI_HackingNode> NodeUIObjects => nodeUIObjects;

	/// <summary>
	/// List of output nodes and input nodes.
	/// </summary>
	private List<GUI_HackingNode> inputNodeUIObjects = new List<GUI_HackingNode>();
	public List<GUI_HackingNode> InputNodeUIObjects => inputNodeUIObjects;

	private List<GUI_HackingNode> outputNodeUIObjects = new List<GUI_HackingNode>();
	public List<GUI_HackingNode> OutputNodeUIObjects => outputNodeUIObjects;

	/// <summary>
	/// List of wires UI objects.
	/// </summary>
	private List<GUI_HackingWire> hackingWires = new List<GUI_HackingWire>();
	public List<GUI_HackingWire> HackingWires => hackingWires;

	/// <summary>
	/// List of hacking device UI objects.
	/// </summary>
	private List<GUI_HackingDevice> hackingDevices = new List<GUI_HackingDevice>();
	public List<GUI_HackingDevice> HackingDevices => hackingDevices;

	[SerializeField]
	private RectTransform inputsLayout;

	[SerializeField]
	private RectTransform outputsLayout;

	[SerializeField]
	private GameObject inputHackingNodeUIPrefab;

	[SerializeField]
	private GameObject outputHackingNodeUIPrefab;

	[SerializeField]
	private GameObject connectingWireUIPrefab;

	[SerializeField]
	private GameObject hackingDeviceUIPrefab;

	[SerializeField]
	private RectTransform hackingDeviceLayout;

	[SerializeField]
	[Tooltip("This is the cell size of the hacking nodes when displayed by the UI. The cell size isn't calculated dynamically, but the spacing betweem them is.")]
	private Vector2 nodeCellSize;

	[SerializeField]
	[Tooltip("This is the cell size of any hacking devices being added.")]
	private Vector2 deviceCellSize;

	[SerializeField]
	private Vector2 deviceNodeCellSize;

	private bool isAddingWire = false;
	public bool IsAddingWire => isAddingWire;


	/// <summary>
	/// These are used interanlly for when a new wire is being added.
	/// </summary>
	private GUI_HackingNode newWireOutput;
	private GUI_HackingNode newWireInput;

	private List<int[]> connectionList = new List<int[]>();

	void Start()
    {
		if (Provider != null)
		{
			hackInterface = Provider.GetComponentInChildren<IHackable>();
			hackProcess = Provider.GetComponentInChildren<HackingProcessBase>();
			hackProcess.RegisterHackingGUI(this);

			SetNodeList(hackProcess.GetHackNodes());
			connectionList = hackProcess.GetNodeConnectionList();

			if (!IsServer)
			{
				RequestHackingNodeConnections.Send(PlayerManager.LocalPlayerScript.gameObject, hackProcess.gameObject);
			}
		}
	}

	public override void OnEnable()
	{
		base.OnEnable();
		if (!IsServer && hackProcess != null)
		{
			RequestHackingNodeConnections.Send(PlayerManager.LocalPlayerScript.gameObject, hackProcess.gameObject);
		}
	}

	public void ServerOnOpened(ConnectedPlayer connectedPlayer)
	{
		//For some reason, this appears to be null the first time it's called.
		ItemStorage hackStorage = hackProcess == null ? Provider.GetComponentInChildren<ItemStorage>() : hackProcess.ItemStorage;
		if (hackStorage != null)
		{
			hackStorage.ServerAddObserverPlayer(connectedPlayer.GameObject);
		}
	}

	public void ServerOnClosed(ConnectedPlayer connectedPlayer)
	{
		ItemStorage hackStorage = hackProcess == null ? Provider.GetComponentInChildren<ItemStorage>() : hackProcess.ItemStorage;
		if (hackStorage != null)
		{
			hackStorage.ServerRemoveObserverPlayer(connectedPlayer.GameObject);
		}
	}

	/// <summary>
	/// Set the list of nodes this component will use. Importantly, this should be sent this information from the server. The client doesn't actually need to know what nodes are attached where.
	/// At least until it opens the UI.
	/// </summary>
	/// <param name="nodeList"></param>
	public void SetNodeList(List<HackingNode> nodeList)
	{
		DeleteOldNodes();
		DeleteOldWires();
		DeleteOldDevices();
		ForceLayoutGroupUpdates();
		hackNodes = nodeList;
		GenerateNodeUI();
	}


	private void SafeDestory(GameObject obj)
	{
		obj.transform.parent = null;
		obj.name = "$disposed";
		Destroy(obj);
		obj.SetActive(false);
	}

	private void RegenerateUI()
	{
		DeleteOldNodes();
		DeleteOldWires();
		DeleteOldDevices();
		GenerateNodeUI();
	}

	private void DeleteOldNodes()
	{
		foreach(GUI_HackingNode UINode in nodeUIObjects)
		{
			SafeDestory(UINode.gameObject);
		}
		nodeUIObjects = new List<GUI_HackingNode>();
		inputNodeUIObjects = new List<GUI_HackingNode>();
		outputNodeUIObjects = new List<GUI_HackingNode>();

		hackNodes = new List<HackingNode>();
		inputNodes = new List<HackingNode>();
		outputNodes = new List<HackingNode>();
	}

	private void ForceLayoutGroupUpdates()
	{
		HorizontalLayoutGroup inputLayout = inputsLayout.GetComponentInChildren<HorizontalLayoutGroup>();
		inputLayout.CalculateLayoutInputHorizontal();
		inputLayout.CalculateLayoutInputVertical();
		inputLayout.SetLayoutHorizontal();
		inputLayout.SetLayoutVertical();

		HorizontalLayoutGroup outputLayout = outputsLayout.GetComponentInChildren<HorizontalLayoutGroup>();
		outputLayout.CalculateLayoutInputHorizontal();
		outputLayout.CalculateLayoutInputVertical();
		outputLayout.SetLayoutHorizontal();
		outputLayout.SetLayoutVertical();

		HorizontalLayoutGroup deviceLayout = hackingDeviceLayout.GetComponent<HorizontalLayoutGroup>();
		deviceLayout.CalculateLayoutInputHorizontal();
		deviceLayout.CalculateLayoutInputVertical();
		deviceLayout.SetLayoutHorizontal();
		deviceLayout.SetLayoutVertical();
	}

	/// <summary>
	/// Generate the UI to represent the hacking nodes. This will be input nodes 
	/// </summary>
	private void GenerateNodeUI()
	{
		foreach (HackingNode node in hackNodes)
		{
			if (node.IsInput)
			{
				inputNodes.Add(node);
			}
			else
			{
				outputNodes.Add(node);
			}
		}
		GenerateInputNodeUI();
		GenerateOutputNodeUI();
		GenerateDeviceNodeUI();

		ForceLayoutGroupUpdates();

		GenerateNodeConnections();
	}

	/// <summary>
	/// Gets the UI component of a node inside the system. Every node should have a UI component, so this shouldn't ever return null. If it does, uh oh.
	/// </summary>
	/// <param name="node"></param>
	/// <returns></returns>
	private GUI_HackingNode GetUIComponentOfNode(HackingNode node)
	{
		return nodeUIObjects.Find(x => x.HackNode.Equals(node));
	}

	private void GenerateInputNodeUI()
	{
		int numNodes = inputNodes.Count(x => !x.IsDeviceNode);
		HorizontalLayoutGroup layout = inputsLayout.GetComponentInChildren<HorizontalLayoutGroup>();
		float cellSizeX = nodeCellSize.x;
		float layoutWidth = inputsLayout.rect.width;
		float spacing = (layoutWidth - cellSizeX * numNodes) / numNodes;

		layout.spacing = spacing;

		foreach (HackingNode node in inputNodes)
		{
			if (node.IsDeviceNode) continue;

			GameObject nodeUIObject = Instantiate(inputHackingNodeUIPrefab, inputsLayout.transform);
			RectTransform nodeRect = nodeUIObject.transform as RectTransform;
			nodeRect.sizeDelta = nodeCellSize;

			GUI_HackingNode nodeGUI = nodeUIObject.GetComponent<GUI_HackingNode>();
			nodeGUI.SetHackingNode(node);

			inputNodeUIObjects.Add(nodeGUI);
			nodeUIObjects.Add(nodeGUI);
		}

	}

	private void GenerateOutputNodeUI()
	{
		int numNodes = outputNodes.Count(x => !x.IsDeviceNode);
		HorizontalLayoutGroup layout = outputsLayout.GetComponentInChildren<HorizontalLayoutGroup>();
		float cellSizeX = nodeCellSize.x;
		float layoutWidth = inputsLayout.rect.width;
		float spacing = (layoutWidth - cellSizeX * numNodes)/numNodes;

		layout.spacing = spacing;

		foreach (HackingNode node in outputNodes)
		{
			if (node.IsDeviceNode) continue;

			GameObject nodeUIObject = Instantiate(outputHackingNodeUIPrefab, outputsLayout.transform);
			RectTransform nodeRect = nodeUIObject.transform as RectTransform;
			nodeRect.sizeDelta = nodeCellSize;

			GUI_HackingNode nodeGUI = nodeUIObject.GetComponent<GUI_HackingNode>();
			nodeGUI.SetHackingNode(node);

			outputNodeUIObjects.Add(nodeGUI);
			nodeUIObjects.Add(nodeGUI);
		}
	}

	private void GenerateDeviceNodeUI()
	{
		Debug.Log("Device NodeUI called.");
	//	foreach (ItemSlot device in hackProcess.Devices)
		foreach (ItemSlot itemSlot in hackProcess.ItemStorage.GetItemSlots())
		{

			HackingDevice device = itemSlot.Item != null ? itemSlot.Item.GetComponent<HackingDevice>() : null;

			if (device == null) continue;

			Debug.Log("Generating device node ui.");
			GameObject devicePanel = Instantiate(hackingDeviceUIPrefab, hackingDeviceLayout);
			RectTransform deviceRect = devicePanel.transform as RectTransform;
			deviceRect.sizeDelta = deviceCellSize;

			GUI_HackingDevice GUIDevice = devicePanel.GetComponent<GUI_HackingDevice>();
			GUIDevice.SetHackingDevice(device);

			ForceLayoutGroupUpdates();

			/////////////////////////Adding Input Node For Device/////////////////////////
			GameObject inputNodeUIObject = Instantiate(inputHackingNodeUIPrefab, GUIDevice.transform);
			RectTransform inNodeRect = inputNodeUIObject.transform as RectTransform;
			inNodeRect.sizeDelta = deviceNodeCellSize;
			inputNodeUIObject.transform.position = (Vector2)devicePanel.transform.position - new Vector2(0, deviceCellSize.y/2);

			GUI_HackingNode inputNodeGUI = inputNodeUIObject.GetComponent<GUI_HackingNode>();
			inputNodeGUI.SetHackingNode(device.InputNode);

			inputNodeUIObjects.Add(inputNodeGUI);
			nodeUIObjects.Add(inputNodeGUI);
			/////////////////////////////////////////////////////////////////////////////


			/////////////////////////Adding Output Node For Device/////////////////////////
			GameObject outputNodeUIObject = Instantiate(outputHackingNodeUIPrefab, GUIDevice.transform);
			RectTransform outNodeRect = outputNodeUIObject.transform as RectTransform;
			outNodeRect.sizeDelta = deviceNodeCellSize;
			outputNodeUIObject.transform.position = (Vector2)devicePanel.transform.position + new Vector2(0, deviceCellSize.y/2);

			GUI_HackingNode outputNodeGUI = outputNodeUIObject.GetComponent<GUI_HackingNode>();
			outputNodeGUI.SetHackingNode(device.OutputNode);

			outputNodeUIObjects.Add(outputNodeGUI);
			nodeUIObjects.Add(outputNodeGUI);
			/////////////////////////////////////////////////////////////////////////////

			hackingDevices.Add(GUIDevice);

		}
	}

	private void GenerateNodeConnections()
	{
		foreach (HackingNode node in outputNodes)
		{
			foreach (HackingNode subNode in node.ConnectedInputNodes)
			{
				GUI_HackingNode outputUINode = GetUIComponentOfNode(node);
				GUI_HackingNode inputUINode = GetUIComponentOfNode(subNode);

				GameObject connectingWire = Instantiate(connectingWireUIPrefab, transform);
				GUI_HackingWire GUIWire = connectingWire.GetComponent<GUI_HackingWire>();
				GUIWire.SetStartUINode(outputUINode);
				GUIWire.SetEndUINode(inputUINode);
				GUIWire.PositionWireBody();

				hackingWires.Add(GUIWire);
			}
		}
	}

	public void DeleteOldWires()
	{
		foreach (GUI_HackingWire wire in hackingWires)
		{
			SafeDestory(wire.gameObject);
		}
		hackingWires = new List<GUI_HackingWire>();
	}

	public void RegenerateWiring()
	{
		DeleteOldWires();
		GenerateNodeConnections();
	}

	public void RemoveWire(GUI_HackingWire wireUI)
	{
		HackingNode outputNode = wireUI.StartNode.HackNode;
		HackingNode inputNode = wireUI.EndNode.HackNode;

		outputNode.RemoveConnectedNode(inputNode);
			
		ClientRecalculateConnectionList();

		//If we're on client, network to the server the changes we made.
		if (!IsServer)
		{
			int outIndex = hackNodes.IndexOf(outputNode);
			int inIndex = hackNodes.IndexOf(inputNode);
			int[] connectionToRemove = { outIndex, inIndex };
			RemoveHackingConnection.Send(PlayerManager.LocalPlayerScript.gameObject, hackProcess.gameObject, connectionToRemove);
			Debug.Log("Client attempting to remove wire.");
		}

		hackingWires.Remove(wireUI);
		Destroy(wireUI.gameObject);
	}

	private void DeleteOldDevices()
	{
		foreach (GUI_HackingDevice device in hackingDevices)
		{
			SafeDestory(device.gameObject);
		}
		hackingDevices = new List<GUI_HackingDevice>();
	}

	//In theory, this should sync devices between server and client. In practice, I worry there may be a race condition, so, watch out for that I guess.

	/// <summary>
	/// Client side function to update devices from observed slots in storage.
	/// </summary>
	public void UpdateDevices()
	{
		if (IsServer) return;
		hackProcess.RemoveAllDevices();

		foreach (ItemSlot deviceSlot in hackProcess.ItemStorage.GetItemSlots())
		{
			Pickupable devicePickup = deviceSlot.Item;
			if (devicePickup != null)
			{
				HackingDevice device = devicePickup.GetComponent<HackingDevice>();
				hackProcess.AddHackingDevice(device);
			}
		}
		//Refresh this since new nodes will have been added/removed.
		SetNodeList(hackProcess.GetHackNodes());
		//RegenerateDevices();
		//RegenerateWiring();
	}

	public void RegenerateDevices()
	{
		DeleteOldDevices();
		GenerateDeviceNodeUI();
	}

	public void RemoveDevice(GUI_HackingDevice deviceUI)
	{
		HackingDevice hackDevice = null;
		foreach( ItemSlot itemSlot in hackProcess.ItemStorage.GetItemSlots())
		{
			if (itemSlot.Item != null && itemSlot.Item.GetComponent<HackingDevice>().Equals(deviceUI.Device))
			{
				Debug.Log("Hacking device found!");
				hackDevice = itemSlot.Item.GetComponent<HackingDevice>();
			}
		}
		
		RemoveHackingDevice.Send(PlayerManager.LocalPlayerScript.gameObject, hackProcess.gameObject, hackDevice.gameObject);
	}
	/// <summary>
	/// This is called when the client changes something about the connection list. Don't trust this output! Only using it to predict UI changes.
	/// </summary>
	private void ClientRecalculateConnectionList()
	{
		connectionList = GetNodeConnectionList();
		Debug.Log("Printing client connection lits:");
	}

	/// <summary>
	/// This method is called by a net message from the server. The client predicts actions will have the determined effect and then this function will
	/// update the UI if there's a mistmatch.
	/// </summary>
	/// <param name="serverConnections"></param>
	public void UpdateConnectionList(List<int[]> serverConnections)
	{
		UpdateDevices();
		Debug.Log("Received new list from server, checking if list is the same.");
		bool sameList = Enumerable.SequenceEqual(connectionList, serverConnections);
		if (!sameList)
		{
			Debug.Log("Connection list mismatch, reloading on client.");
			connectionList = serverConnections;
			UpdateNodeConnectionsFromConnectionList();
			RegenerateWiring();
		}
	}

	/// <summary>
	/// Update what nodes are connected to what using the connection list.
	/// </summary>
	private void UpdateNodeConnectionsFromConnectionList()
	{
		foreach (HackingNode node in hackNodes)
		{
			node.RemoveAllConnectedNodes();
		}

		foreach (int[] connection in connectionList)
		{
			HackingNode outputNode = hackNodes[connection[0]];
			HackingNode inputNode = hackNodes[connection[1]];

			outputNode.AddConnectedNode(inputNode);
		}
	}

	public  List<int[]> GetNodeConnectionList()
	{
		List<int[]> connectionList = new List<int[]>();
		List<HackingNode> hackingNodes = hackNodes;
		int outputIndex = 0;
		foreach (HackingNode node in hackingNodes)
		{
			List<HackingNode> connectedNodes = node.ConnectedInputNodes;
			foreach (HackingNode connectedNode in connectedNodes)
			{
				int inputIndex = hackingNodes.IndexOf(connectedNode);
				int[] connection = { outputIndex, inputIndex };
				connectionList.Add(connection);
			}
			outputIndex++;
		}
		return connectionList;
	}

	public void BeginAddingWire(GUI_HackingNode outputNode)
	{
		newWireOutput = outputNode;
		isAddingWire = true;
	}

	public void FinishAddingWire(GUI_HackingNode inputNode)
	{
		newWireInput = inputNode;

		newWireOutput.HackNode.AddConnectedNode(newWireInput.HackNode);

		ClientRecalculateConnectionList();

		if (!IsServer)
		{
			int outIndex = hackNodes.IndexOf(newWireOutput.HackNode);
			int inIndex = hackNodes.IndexOf(newWireInput.HackNode);
			int[] connectionToAdd = { outIndex, inIndex };
			AddHackingConnection.Send(PlayerManager.LocalPlayerScript.gameObject, hackProcess.gameObject, connectionToAdd);

			Debug.Log("Client attempting to add wire.");
		}

		RegenerateWiring();

		isAddingWire = false;

	}

	public void AttemptAddDevice()
	{
		Debug.Log("Attempting to add device.");
		Pickupable handItem = PlayerManager.LocalPlayerScript.Equipment.ItemStorage.GetActiveHandSlot().Item;
		if (handItem != null)
		{
			Debug.Log("Item in hand, checking if valid hack device.");
			HackingDevice hackDevice = handItem.GetComponent<HackingDevice>();
			if (hackDevice != null)
			{
				Debug.Log("Valid Hacking Device, adding now.");
				AddHackingDevice.Send(PlayerManager.LocalPlayerScript.gameObject, hackProcess.gameObject, hackDevice.gameObject);
			}
		}
	}
}
