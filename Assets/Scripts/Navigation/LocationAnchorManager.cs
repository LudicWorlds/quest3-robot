using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System;



public class LocationAnchorManager : MonoBehaviour
{
    [Header("Spatial Anchors")]
    [SerializeField] private OVRSpatialAnchor _locationAnchorPrefab;
    private List<OVRSpatialAnchor> _locationAnchors = new();
    private int _currentLocAnchorIndex = -1; // -1 means no anchor selected
    private int _currentLabelIndex = 0; // Current label being assigned to selected anchor

    private string[] _labels = { LocationLabel.FRIDGE,
                                 LocationLabel.SOFA,
                                 LocationLabel.TABLE };

    private EventBroker _eventBroker;

    private void Awake()
    {
        _eventBroker = EventBroker.GetInstance();
        _eventBroker.CreateEventHandler(EventID.SPATIAL_ANCHORS_LOADED);
    }

    public void DestroyAllLocationAnchors()
    {
        if (_locationAnchors.Count == 0)
        {
            Debug.Log("No Location Anchors to destroy.");
            return;
        }

        Debug.Log($"Destroying {_locationAnchors.Count} Location Anchors...");
        StartCoroutine(DestroyAllLocationAnchorsCoroutine());
    }



    public void CreateSpatialAnchor(Vector3 hitPoint, Quaternion rotation)
    {
        // Create new anchor at new position
        OVRSpatialAnchor newAnchor = Instantiate(_locationAnchorPrefab, hitPoint, rotation);
        StartCoroutine(AnchorCreated(newAnchor));
    }

    private IEnumerator AnchorCreated(OVRSpatialAnchor newAnchor)
    {
        while (!newAnchor.Created && !newAnchor.Localized)
        {
            yield return new WaitForEndOfFrame();
        }

        Guid anchorGuid = newAnchor.Uuid;
        TMP_Text uuidText = newAnchor.transform.GetChild(1).GetComponent<TMP_Text>();
        TMP_Text labelText = newAnchor.transform.GetChild(2).GetComponent<TMP_Text>();

        if (uuidText != null)
            uuidText.text = "UUID: " + anchorGuid.ToString();
        else
            Debug.Log("-> Can't find uuidText! :(");

        if (labelText != null)
            labelText.text = _labels[0];
        else
            Debug.Log("-> Can't find labelText! :(");


        if (_currentLocAnchorIndex >= 0 && _currentLocAnchorIndex < _locationAnchors.Count)
        {
            // Replace in the list
            _locationAnchors[_currentLocAnchorIndex] = newAnchor;
            Debug.Log($"Moved anchor {_currentLocAnchorIndex} to new position");
        }
        else
        {
            // Add to tracking list
            _locationAnchors.Add(newAnchor);
            _currentLocAnchorIndex = _locationAnchors.Count - 1; // Focus on newly placed anchor
            Debug.Log($"Created new anchor at index {_currentLocAnchorIndex}");
        }

        HighlightCurrentAnchor();
    }

    private IEnumerator DestroyAllLocationAnchorsCoroutine()
    {
        int erasedCount = 0;
        int failedCount = 0;

        // Create a copy of the list to iterate over (since we'll be modifying the original)
        List<OVRSpatialAnchor> anchorsToDestroy = new List<OVRSpatialAnchor>(_locationAnchors);

        foreach (OVRSpatialAnchor anchor in anchorsToDestroy)
        {
            if (anchor == null)
            {
                Debug.LogWarning("Skipping null anchor during destruction");
                failedCount++;
                continue;
            }

            // Get info before erasing
            Guid uuid = anchor.Uuid;
            string label = "Unknown";
            TMP_Text labelText = anchor.transform.GetChild(2).GetComponent<TMP_Text>();
            if (labelText != null)
            {
                label = labelText.text;
            }

            // Erase the anchor from Meta's persistent storage
            var eraseTask = anchor.EraseAnchorAsync();

            // Wait for erase to complete
            while (!eraseTask.IsCompleted)
            {
                yield return null;
            }

            var result = eraseTask.GetResult();
            if (result.Success)
            {
                Debug.Log($"✓ Erased anchor from storage: {label} (UUID: {uuid})");
                erasedCount++;
            }
            else
            {
                Debug.LogError($"✗ Failed to erase anchor: {label} (UUID: {uuid}) - Status: {result.Status}");
                failedCount++;
            }

            // Destroy the GameObject regardless of erase success
            Destroy(anchor.gameObject);
        }

        // Clear the anchor list
        _locationAnchors.Clear();
        _currentLocAnchorIndex = -1;

        // Clear all PlayerPrefs data
        ClearAnchorMetadata();

        Debug.Log($"Destroy complete! Erased: {erasedCount}, Failed: {failedCount}");
    }

    private void ClearAnchorMetadata()
    {
        // Get the count to know how many to delete
        int count = PlayerPrefs.GetInt("SpatialAnchorCount", 0);

        // Delete all UUID and label entries
        for (int i = 0; i < count; i++)
        {
            string uuidString = PlayerPrefs.GetString($"SpatialAnchorUUID_{i}", "");
            if (!string.IsNullOrEmpty(uuidString) && Guid.TryParse(uuidString, out Guid uuid))
            {
                PlayerPrefs.DeleteKey($"SpatialAnchorLabel_{uuid}");
            }
            PlayerPrefs.DeleteKey($"SpatialAnchorUUID_{i}");
        }

        // Delete the count
        PlayerPrefs.DeleteKey("SpatialAnchorCount");
        PlayerPrefs.Save();

        Debug.Log($"Cleared all anchor metadata from PlayerPrefs ({count} entries)");
    }

    //---------------------------------------------
    // Spatial Anchor Management
    //---------------------------------------------

    /// <summary>
    /// Selects the nearest spatial anchor that matches the given label
    /// </summary>
    /// <param name="label">The label to search for (e.g., "Fridge", "Sofa", "Table")</param>
    /// <param name="robotPos">Current position of the robot</param>
    /// <returns>True if a matching anchor was found and selected, false otherwise</returns>
    public bool SelectNearestAnchorWithLabel(string label, Vector3 robotPos)
    {
        if (string.IsNullOrEmpty(label))
        {
            Debug.LogWarning("[LocationAnchorManager] Cannot select anchor with null/empty label");
            return false;
        }

        if (_locationAnchors.Count == 0)
        {
            Debug.LogWarning("[LocationAnchorManager] No anchors available to select");
            return false;
        }

        float nearestDistance = float.MaxValue;
        int nearestIndex = -1;

        // Find all anchors with matching label and select the nearest one
        for (int i = 0; i < _locationAnchors.Count; i++)
        {
            OVRSpatialAnchor anchor = _locationAnchors[i];
            if (anchor != null)
            {
                // Get the label text from the anchor
                TMP_Text labelText = anchor.transform.GetChild(2).GetComponent<TMP_Text>();

                if (labelText != null &&
                    labelText.text.Trim().Equals(label.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    // Calculate distance (horizontal only, ignoring Y)
                    Vector3 anchorPos = anchor.transform.position;
                    Vector3 robotPosFlat = new Vector3(robotPos.x, 0, robotPos.z);
                    Vector3 anchorPosFlat = new Vector3(anchorPos.x, 0, anchorPos.z);

                    float distance = Vector3.Distance(robotPosFlat, anchorPosFlat);

                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestIndex = i;
                    }
                }
            }
        }

        // If we found a matching anchor, select it
        if (nearestIndex >= 0)
        {
            _currentLocAnchorIndex = nearestIndex;
            HighlightCurrentAnchor();
            Debug.Log($"[LocationAnchorManager] Selected nearest '{label}' anchor at index {nearestIndex} (distance: {nearestDistance:F2}m)");
            return true;
        }
        else
        {
            Debug.LogWarning($"[LocationAnchorManager] No anchor found with label: {label}");
            return false;
        }
    }

    public void CycleToNextAnchor()
    {
        _currentLocAnchorIndex++;

        // Allow cycling one position beyond the last anchor (for creating new ones)
        if (_currentLocAnchorIndex > _locationAnchors.Count) // One beyond the last anchor
        {
            _currentLocAnchorIndex = 0; // Wrap back to first anchor
        }

        HighlightCurrentAnchor();

        if (_currentLocAnchorIndex < _locationAnchors.Count)
        {
            Debug.Log($"Selected anchor {_currentLocAnchorIndex + 1}/{_locationAnchors.Count}");
        }
        else
        {
            Debug.Log($"Empty slot - press grip to create new anchor");
        }
    }

    public void CycleToPreviousAnchor()
    {
        _currentLocAnchorIndex--;

        // Allow wrapping from first anchor to empty slot, then to last anchor
        if (_currentLocAnchorIndex < 0)
        {
            _currentLocAnchorIndex = _locationAnchors.Count; // Go to empty slot
        }

        HighlightCurrentAnchor();

        if (_currentLocAnchorIndex < _locationAnchors.Count)
        {
            Debug.Log($"Selected anchor {_currentLocAnchorIndex + 1}/{_locationAnchors.Count}");
        }
        else
        {
            Debug.Log($"Empty slot - press grip to create new anchor");
        }
    }

    private void HighlightCurrentAnchor()
    {
        // Reset all anchors to default appearance
        for (int i = 0; i < _locationAnchors.Count; i++)
        {
            if (_locationAnchors[i] != null)
            {
                Renderer renderer = _locationAnchors[i].transform.GetChild(0).GetComponent<Renderer>();
                if (renderer != null)
                {
                    // Default scale/color for non-selected anchors
                    //m_placedAnchors[i].transform.localScale = Vector3.one;
                    renderer.material.color = Color.red;
                }
            }
        }

        // Highlight the current anchor
        if (_currentLocAnchorIndex >= 0 && _currentLocAnchorIndex < _locationAnchors.Count)
        {
            OVRSpatialAnchor currentAnchor = _locationAnchors[_currentLocAnchorIndex];
            if (currentAnchor != null)
            {
                Renderer renderer = currentAnchor.transform.GetChild(0).GetComponent<Renderer>();
                if (renderer != null)
                {
                    // Make it larger and brighter
                    //currentAnchor.transform.localScale = Vector3.one * 1.5f;
                    renderer.material.color = Color.orange;
                }
                else
                {
                    Debug.Log("-> Cant't find NavAnchor's renderer! :(");
                }
            }
        }
    }

    //public void Select

    public void PlaceLocationAnchor(Vector3 hitPoint, Quaternion rotation)
    {
        // Check if we're on an existing anchor or an empty slot
        if (_currentLocAnchorIndex >= 0 && _currentLocAnchorIndex < _locationAnchors.Count)
        {
            // Move existing anchor - need to destroy and recreate due to OVRSpatialAnchor behavior
            OVRSpatialAnchor existingAnchor = _locationAnchors[_currentLocAnchorIndex];

            // Destroy the old anchor
            Destroy(existingAnchor.gameObject);

            CreateSpatialAnchor(hitPoint, rotation);
        }
        else
        {
            CreateSpatialAnchor(hitPoint, rotation);
        }
    }

    /// <summary>
    /// Gets the currently selected anchor and its label
    /// </summary>
    /// <returns>Tuple containing (anchor, label). Both will be null if no anchor is selected.</returns>
    public (OVRSpatialAnchor anchor, string label) GetSelectedAnchor()
    {
        if (_currentLocAnchorIndex < 0 || _currentLocAnchorIndex >= _locationAnchors.Count)
            return (null, null);

        OVRSpatialAnchor anchor = _locationAnchors[_currentLocAnchorIndex];
        TMP_Text labelText = anchor?.transform.GetChild(2).GetComponent<TMP_Text>();
        string label = labelText?.text;

        return (anchor, label);
    }

    //---------------------------------------------
    // Label Management for Selected Anchor
    //---------------------------------------------

    public void CycleLabelForward()
    {
        if (_currentLocAnchorIndex < 0 || _currentLocAnchorIndex >= _locationAnchors.Count)
        {
            Debug.Log("No anchor selected - cannot cycle label");
            return;
        }

        _currentLabelIndex = (_currentLabelIndex + 1) % _labels.Length;
        UpdateSelectedAnchorLabel();
        Debug.Log($"Cycled to label: {_labels[_currentLabelIndex]}");
    }

    public void CycleLabelBackward()
    {
        if (_currentLocAnchorIndex < 0 || _currentLocAnchorIndex >= _locationAnchors.Count)
        {
            Debug.Log("No anchor selected - cannot cycle label");
            return;
        }

        _currentLabelIndex--;
        if (_currentLabelIndex < 0)
            _currentLabelIndex = _labels.Length - 1;

        UpdateSelectedAnchorLabel();
        Debug.Log($"Cycled to label: {_labels[_currentLabelIndex]}");
    }

    private void UpdateSelectedAnchorLabel()
    {
        OVRSpatialAnchor selectedAnchor = _locationAnchors[_currentLocAnchorIndex];
        if (selectedAnchor != null)
        {
            TMP_Text labelText = selectedAnchor.transform.GetChild(2).GetComponent<TMP_Text>();
            if (labelText != null)
            {
                labelText.text = _labels[_currentLabelIndex];
            }
            else
            {
                Debug.LogWarning("Could not find label text component on selected anchor");
            }
        }
    }

    //---------------------------------------------
    // Spatial Anchor Persistence (Save/Load)
    //---------------------------------------------

    public void SaveAllSpatialAnchors()
    {
        if (_locationAnchors.Count == 0)
        {
            Debug.LogWarning("No anchors to save!");
            return;
        }

        Debug.Log($"Starting save process for {_locationAnchors.Count} anchors...");

        // Start the save coroutine for all anchors
        _ = StartCoroutine(SaveAnchorsCoroutine());
    }

    private IEnumerator SaveAnchorsCoroutine()
    {
        int savedCount = 0;
        int failedCount = 0;
        List<Guid> savedUuids = new List<Guid>();
        Dictionary<Guid, string> uuidToLabel = new Dictionary<Guid, string>();

        foreach (OVRSpatialAnchor anchor in _locationAnchors)
        {
            if (anchor == null)
            {
                Debug.LogWarning("Skipping null anchor");
                failedCount++;
                continue;
            }

            // Get the label for this anchor
            string label = "Unknown";
            TMP_Text labelText = anchor.transform.GetChild(2).GetComponent<TMP_Text>();
            if (labelText != null)
            {
                label = labelText.text;
            }

            // Save the anchor using the current API (SaveAnchorAsync)
            var saveTask = anchor.SaveAnchorAsync();

            // Wait for the save task to complete
            while (!saveTask.IsCompleted)
            {
                yield return null;
            }

            // Get the result and check if it succeeded
            var result = saveTask.GetResult();
            if (result.Success)
            {
                Debug.Log($"✓ Saved anchor: {label} (UUID: {anchor.Uuid})");
                savedUuids.Add(anchor.Uuid);
                uuidToLabel[anchor.Uuid] = label;
                savedCount++;
            }
            else
            {
                Debug.LogError($"✗ Failed to save anchor: {label} (UUID: {anchor.Uuid}) - Status: {result.Status}");
                failedCount++;
            }
        }

        // Save UUIDs and labels to PlayerPrefs
        if (savedCount > 0)
        {
            SaveAnchorMetadata(savedUuids, uuidToLabel);
        }

        Debug.Log($"Save complete! Saved: {savedCount}, Failed: {failedCount}");

    }

    private void SaveAnchorMetadata(List<Guid> uuids, Dictionary<Guid, string> uuidToLabel)
    {
        // Save the count
        PlayerPrefs.SetInt("SpatialAnchorCount", uuids.Count);

        // Save each UUID and label
        for (int i = 0; i < uuids.Count; i++)
        {
            Guid uuid = uuids[i];
            PlayerPrefs.SetString($"SpatialAnchorUUID_{i}", uuid.ToString());
            PlayerPrefs.SetString($"SpatialAnchorLabel_{uuid}", uuidToLabel[uuid]);
        }

        PlayerPrefs.Save();
        Debug.Log($"Saved {uuids.Count} anchor UUIDs and labels to PlayerPrefs");
    }

    //---------------------------------------------

    public IEnumerator LoadAllSpatialAnchors()
    {
        Debug.Log("Starting to load saved spatial anchors...");

        // Load saved UUIDs from PlayerPrefs
        List<Guid> savedUuids = LoadSavedAnchorUuids();

        if (savedUuids.Count == 0)
        {
            Debug.Log("No saved spatial anchor UUIDs found.");
            yield break;
        }

        Debug.Log($"Found {savedUuids.Count} saved UUIDs. Loading anchors...");

        // Create list to hold unbound anchors
        List<OVRSpatialAnchor.UnboundAnchor> unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();

        // Load the anchors using the UUIDs
        var loadTask = OVRSpatialAnchor.LoadUnboundAnchorsAsync(savedUuids, unboundAnchors);

        // Wait for load to complete
        while (!loadTask.IsCompleted)
        {
            yield return null;
        }

        var loadResult = loadTask.GetResult();
        if (!loadResult.Success)
        {
            Debug.LogWarning($"Failed to load spatial anchors: {loadResult.Status}");
            yield break;
        }

        Debug.Log($"Loaded {unboundAnchors.Count} unbound anchors. Localizing...");

        int loadedCount = 0;
        int failedCount = 0;

        foreach (var unboundAnchor in unboundAnchors)
        {
            // Instantiate the anchor prefab
            OVRSpatialAnchor newAnchor = Instantiate(_locationAnchorPrefab);

            // Get the UUID before binding
            Guid uuid = unboundAnchor.Uuid;

            // Bind the unbound anchor to the OVRSpatialAnchor component
            unboundAnchor.BindTo(newAnchor);

            // Wait for localization
            while (!newAnchor.Localized)
            {
                yield return null;
            }

            // Add to our tracking list
            _locationAnchors.Add(newAnchor);

            // Update UI components
            TMP_Text uuidText = newAnchor.transform.GetChild(1).GetComponent<TMP_Text>();
            if (uuidText != null)
            {
                uuidText.text = "UUID: " + uuid.ToString();
            }

            // Load the label from PlayerPrefs
            string label = LoadAnchorLabel(uuid);
            TMP_Text labelText = newAnchor.transform.GetChild(2).GetComponent<TMP_Text>();
            if (labelText != null)
            {
                labelText.text = label;
            }

            Debug.Log($"✓ Loaded anchor: {label} (UUID: {uuid})");
            loadedCount++;
        }

        Debug.Log($"Load complete! Loaded: {loadedCount}, Failed: {failedCount}");

        // Highlight the first anchor if any were loaded
        if (_locationAnchors.Count > 0)
        {
            _currentLocAnchorIndex = 0;
            HighlightCurrentAnchor();
        }

        // Dispatch event to signal that spatial anchors are loaded
        _eventBroker.DispatchEvent(EventID.SPATIAL_ANCHORS_LOADED);
    }

    private List<Guid> LoadSavedAnchorUuids()
    {
        List<Guid> uuids = new List<Guid>();

        // Load the count of saved anchors
        int count = PlayerPrefs.GetInt("SpatialAnchorCount", 0);

        for (int i = 0; i < count; i++)
        {
            string uuidString = PlayerPrefs.GetString($"SpatialAnchorUUID_{i}", "");
            if (!string.IsNullOrEmpty(uuidString) && Guid.TryParse(uuidString, out Guid uuid))
            {
                uuids.Add(uuid);
            }
        }

        return uuids;
    }

    private string LoadAnchorLabel(Guid uuid)
    {
        return PlayerPrefs.GetString($"SpatialAnchorLabel_{uuid}", "Unknown");
    }


    /// <summary>
    /// Find an anchor by its label text (case-insensitive)
    /// Returns the index of the anchor, or -1 if not found
    /// </summary>
    public int FindAnchorByLabel(string label)
    {
        if (string.IsNullOrEmpty(label))
        {
            Debug.LogWarning("[RobotController] Cannot find anchor with null/empty label");
            return -1;
        }

        for (int i = 0; i < _locationAnchors.Count; i++)
        {
            OVRSpatialAnchor anchor = _locationAnchors[i];
            if (anchor != null)
            {
                TMP_Text labelText = anchor.transform.GetChild(2).GetComponent<TMP_Text>();
                if (labelText != null &&
                    labelText.text.Trim().Equals(label.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[RobotController] Found anchor with label '{label}' at index {i}");
                    return i;
                }
            }
        }

        Debug.LogWarning($"[RobotController] No anchor found with label: {label}");
        return -1;
    }

    /// <summary>
    /// Set the current location anchor index (for voice commands)
    /// </summary>
    public void SetCurrentLocationAnchorIndex(int index)
    {
        if (index >= 0 && index < _locationAnchors.Count)
        {
            _currentLocAnchorIndex = index;
            HighlightCurrentAnchor();
            Debug.Log($"[RobotController] Current Anchor set to index: {index}");
        }
        else
        {
            Debug.LogWarning($"[RobotController] Invalid Anchor index: {index}");
        }
    }
}
