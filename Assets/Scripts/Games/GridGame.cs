using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GridMemoryGame : MonoBehaviour
{
#if !UNITY_WEBGL
    private UDPManager udp;
#endif

    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI instructionsText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI feedbackText;
    public Slider progressBar;
    
    [Header("Grid Settings")]
    public GameObject gridContainer;
    public GameObject cellPrefab;
    public int gridSize = 4;
    public float cellSpacing = 10f;
    public int startingSequenceLength = 2;
    public int maxLevel = 8;
    
    [Header("Timing Settings")]
    public float patternDisplayTime = 1.0f;
    public float betweenCellDelay = 0.5f;
    public float memorizationTime = 1.0f;
    public float responseTimeLimit = 10.0f;
    
    [Header("Colors")]
    public Color idleColor = Color.gray;
    public Color activeColor = Color.yellow;
    public Color correctColor = Color.green;
    public Color wrongColor = Color.red;
    public Color highlightColor = new Color(0.2f, 0.8f, 1f);
    public Color startColor = new Color(0.5f, 0.5f, 1f);
    
    private int currentLevel = 1;
    private int currentScore = 0;
    private int maxPossibleScore = 0;
    private int sequenceLength = 2;
    private bool gameActive = false;
    private bool showingPattern = false;
    private bool acceptingInput = false;
    private bool waitingForStart = true;
    private GameObject[,] gridCells;
    private Button[,] cellButtons;
    private Image[,] cellImages;
    private List<Vector2Int> currentPattern = new List<Vector2Int>();
    private List<Vector2Int> playerInput = new List<Vector2Int>();
    private List<float> responseTimes = new List<float>();
    private List<int> levelScores = new List<int>();
    private float levelStartTime;
    private List<string> gameLog = new List<string>();

    void Start()
    {
#if !UNITY_WEBGL
        udp = FindObjectOfType<UDPManager>();
#endif
        InitializeGame();
        CreateGrid();
        SetupStartScreen();
    }

    void InitializeGame()
    {
        sequenceLength = startingSequenceLength;
        maxPossibleScore = 0;
        for (int i = 0; i < maxLevel; i++)
        {
            int seq = startingSequenceLength + i;
            maxPossibleScore += seq * 1;
        }
        
        if (titleText != null) titleText.text = "CIRCUIT REPAIR";
        if (instructionsText != null) instructionsText.text = "Memorize the pattern, then tap the cells in the same order.";
        if (scoreText != null) scoreText.text = $"Score: 0/{maxPossibleScore}";
        if (levelText != null) levelText.text = "Level: 1";
        if (feedbackText != null) feedbackText.text = "";
        
        if (progressBar != null)
        {
            progressBar.minValue = 1;
            progressBar.maxValue = maxLevel;
            progressBar.value = 1;
        }
    }

    void SetupStartScreen()
    {
        if (gridContainer != null) gridContainer.SetActive(true);
        for (int row = 0; row < gridSize; row++)
            for (int col = 0; col < gridSize; col++)
                if (cellImages[row, col] != null) cellImages[row, col].color = startColor;
        EnableGridInput(true);
        if (feedbackText != null)
        {
            feedbackText.text = "Tap any cell to begin...";
            feedbackText.color = Color.yellow;
            feedbackText.fontSize = 32;
            RectTransform rect = feedbackText.GetComponent<RectTransform>();
            if (rect != null) rect.anchoredPosition = new Vector2(0, 100);
        }
        waitingForStart = true;
    }

    void CreateGrid()
    {
        if (gridContainer == null || cellPrefab == null) return;
        foreach (Transform child in gridContainer.transform) Destroy(child.gameObject);
        gridCells = new GameObject[gridSize, gridSize];
        cellButtons = new Button[gridSize, gridSize];
        cellImages = new Image[gridSize, gridSize];
        GridLayoutGroup gridLayout = gridContainer.GetComponent<GridLayoutGroup>();
        if (gridLayout == null) gridLayout = gridContainer.AddComponent<GridLayoutGroup>();
        float containerSize = 600f;
        float cellSize = (containerSize - (cellSpacing * (gridSize - 1))) / gridSize;
        gridLayout.cellSize = new Vector2(cellSize, cellSize);
        gridLayout.spacing = new Vector2(cellSpacing, cellSpacing);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = gridSize;
        gridLayout.childAlignment = TextAnchor.MiddleCenter;
        for (int row = 0; row < gridSize; row++)
            for (int col = 0; col < gridSize; col++)
            {
                GameObject cell = Instantiate(cellPrefab, gridContainer.transform);
                cell.name = $"Cell_{row}_{col}";
                gridCells[row, col] = cell;
                cellButtons[row, col] = cell.GetComponent<Button>();
                cellImages[row, col] = cell.GetComponent<Image>();
                int r = row, c = col;
                cellButtons[row, col].onClick.AddListener(() => OnCellClicked(r, c));
                cellImages[row, col].color = idleColor;
                cellButtons[row, col].interactable = false;
            }
    }

    void StartGame()
    {
        gameActive = true;
        waitingForStart = false;
        if (feedbackText != null) feedbackText.text = "";
        ResetAllCells();
        EnableGridInput(false);
        gameLog.Add("Spatial Memory Game Started: " + System.DateTime.Now.ToString());
#if !UNITY_WEBGL
        if (udp != null) udp.SendMarker("Corsi_Start");
#endif
        StartCoroutine(LevelRoutine());
    }

    IEnumerator LevelRoutine()
    {
        if (levelText != null) levelText.text = $"Level: {currentLevel}";
        if (progressBar != null) progressBar.value = currentLevel;
#if !UNITY_WEBGL
        if (udp != null) udp.SendMarker($"Corsi_Level:{sequenceLength}");
#endif
        if (feedbackText != null) { feedbackText.text = $"Level {currentLevel}: Memorize {sequenceLength} cells..."; feedbackText.color = Color.yellow; }
        yield return new WaitForSeconds(1f);
#if !UNITY_WEBGL
        if (udp != null) udp.SendMarker("Corsi_PatternStart");
#endif
        GeneratePattern(sequenceLength);
        yield return StartCoroutine(ShowPattern());
#if !UNITY_WEBGL
        if (udp != null) udp.SendMarker("Corsi_PatternEnd");
#endif
        if (feedbackText != null) { feedbackText.text = "Now repeat the pattern..."; feedbackText.color = Color.cyan; }
        yield return new WaitForSeconds(memorizationTime);
        if (feedbackText != null) feedbackText.text = "";
        acceptingInput = true;
        EnableGridInput(true);
        playerInput.Clear();
        levelStartTime = Time.time;
#if !UNITY_WEBGL
        if (udp != null) udp.SendMarker("Corsi_ResponseStart");
#endif
        while (acceptingInput && playerInput.Count < sequenceLength)
        {
            if (Time.time - levelStartTime > responseTimeLimit) { Timeout(); yield break; }
            if (feedbackText != null)
            {
                float timeLeft = responseTimeLimit - (Time.time - levelStartTime);
                feedbackText.text = $"Time: {timeLeft:F1}s";
                feedbackText.color = timeLeft < 3f ? Color.red : Color.white;
            }
            yield return null;
        }
    }

    void GeneratePattern(int length)
    {
        currentPattern.Clear();
        while (currentPattern.Count < length)
        {
            Vector2Int newCell = new Vector2Int(Random.Range(0, gridSize), Random.Range(0, gridSize));
            if (!currentPattern.Contains(newCell)) currentPattern.Add(newCell);
        }
    }

    IEnumerator ShowPattern()
    {
        showingPattern = true;
        foreach (Vector2Int cell in currentPattern)
        {
            cellImages[cell.x, cell.y].color = activeColor;
            yield return new WaitForSeconds(patternDisplayTime);
            cellImages[cell.x, cell.y].color = idleColor;
            yield return new WaitForSeconds(betweenCellDelay);
        }
        showingPattern = false;
    }

    void OnCellClicked(int row, int col)
    {
        if (waitingForStart) { StartGame(); return; }
        if (!gameActive || !acceptingInput || showingPattern) return;
        Vector2Int clickedCell = new Vector2Int(row, col);
        playerInput.Add(clickedCell);
#if !UNITY_WEBGL
        if (udp != null) udp.SendMarker($"Corsi_Click:{row},{col}");
#endif
        cellImages[row, col].color = highlightColor;
        if (playerInput.Count == sequenceLength)
        {
            acceptingInput = false;
            EnableGridInput(false);
            StartCoroutine(CheckPattern());
        }
    }

    IEnumerator CheckPattern()
    {
        float responseTime = Time.time - levelStartTime;
        responseTimes.Add(responseTime);
        bool correct = true;
        for (int i = 0; i < sequenceLength; i++) if (playerInput[i] != currentPattern[i]) { correct = false; break; }
#if !UNITY_WEBGL
        if (udp != null)
        {
            udp.SendMarker(correct ? "Corsi_Correct" : "Corsi_Incorrect");
            udp.SendMarker($"Corsi_LevelEnd:{correct}");
        }
#endif
        for (int i = 0; i < sequenceLength; i++)
        {
            Vector2Int cell = currentPattern[i];
            cellImages[cell.x, cell.y].color = correct ? correctColor : wrongColor;
        }
        if (feedbackText != null) feedbackText.text = correct ? $"Correct! Time: {responseTime:F2}s" : "Incorrect pattern";
        if (feedbackText != null) feedbackText.color = correct ? correctColor : wrongColor;
        yield return new WaitForSeconds(2f);
        ResetAllCells();
        if (correct)
        {
            currentScore += sequenceLength * 1;
            if (scoreText != null) scoreText.text = $"Score: {currentScore}/{maxPossibleScore}";
            if (currentLevel < maxLevel)
            {
                currentLevel++;
                sequenceLength++;
                levelScores.Add(sequenceLength);
                StartCoroutine(LevelRoutine());
            }
            else EndGame(true);
        }
        else EndGame(false);
    }

    void Timeout()
    {
        acceptingInput = false;
        EnableGridInput(false);
#if !UNITY_WEBGL
        if (udp != null) udp.SendMarker("Corsi_Timeout");
#endif
        if (feedbackText != null) { feedbackText.text = "Time's up!"; feedbackText.color = wrongColor; }
        StartCoroutine(ShowTimeoutPattern());
    }

    IEnumerator ShowTimeoutPattern()
    {
        yield return StartCoroutine(ShowPattern());
        yield return new WaitForSeconds(1f);
        EndGame(false);
    }

    void EndGame(bool completedAllLevels)
    {
        gameActive = false;
        acceptingInput = false;
#if !UNITY_WEBGL
        if (udp != null)
        {
            udp.SendMarker("Corsi_End");
            udp.SendMarker($"Corsi_FinalScore:{currentScore}");
            udp.SendMarker($"Corsi_MaxLevel:{currentLevel}");
            udp.SendMarker($"Corsi_MaxSequence:{sequenceLength}");
        }
#endif
        float avgResponseTime = responseTimes.Count > 0 ? responseTimes.Average() : 0;
        int maxLevelReached = currentLevel;
        int maxSequenceLength = sequenceLength;
        if (gridContainer != null) gridContainer.SetActive(false);

        // Hide all game UI
        if (instructionsText != null) instructionsText.gameObject.SetActive(false);
        if (scoreText != null) scoreText.gameObject.SetActive(false);
        if (levelText != null) levelText.gameObject.SetActive(false);
        
        // Set end screen text
        feedbackText.text =
            $"TEST COMPLETE\n\n" +
            $"Max Sequence: {maxSequenceLength} cells\n" +
            $"Levels Completed: {maxLevelReached}/{maxLevel}\n" +
            $"Final Score: {currentScore}/{maxPossibleScore}\n" +
            $"Average Time: {avgResponseTime:F2}s";
        feedbackText.fontSize = 48;
        feedbackText.color = Color.cyan;
        feedbackText.alignment = TextAlignmentOptions.Center;

        // Force center alignment
        RectTransform rect = feedbackText.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        if (titleText != null) titleText.gameObject.SetActive(false);

        SaveGameData(maxSequenceLength, avgResponseTime, completedAllLevels);
        StartCoroutine(LoadNextGameAfterDelay(3f));
    }

    IEnumerator LoadNextGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        int nextIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex + 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextIndex);
    }

    void EnableGridInput(bool enable)
    {
        if (cellButtons == null) return;
        for (int row = 0; row < gridSize; row++)
            for (int col = 0; col < gridSize; col++)
                if (cellButtons[row, col] != null) cellButtons[row, col].interactable = enable;
    }

    void ResetAllCells()
    {
        if (cellImages == null) return;
        for (int row = 0; row < gridSize; row++)
            for (int col = 0; col < gridSize; col++)
                if (cellImages[row, col] != null) cellImages[row, col].color = idleColor;
    }

    void SaveGameData(int maxSequence, float avgTime, bool completed)
    {
        string filePath = Application.persistentDataPath + "/SpatialMemoryData.txt";
        string data = "=== SPATIAL MEMORY RESULTS ===\n";
        data += "Date: " + System.DateTime.Now + "\n";
        data += "Max Sequence Length: " + maxSequence + "\n";
        data += "Levels Completed: " + currentLevel + "/" + maxLevel + "\n";
        data += "Final Score: " + currentScore + "/" + maxPossibleScore + "\n";
        data += "Average Response Time: " + avgTime.ToString("F2") + "s\n";
        data += "Game Completed: " + (completed ? "YES" : "NO") + "\n";
        data += "\nResponse Times:\n";
        for (int i = 0; i < responseTimes.Count; i++) data += $"Level {i+1}: {responseTimes[i]:F2}s\n";
        data += "\nGame Log:\n";
        foreach (string log in gameLog) data += log + "\n";
        System.IO.File.WriteAllText(filePath, data);
        Debug.Log("Spatial memory data saved to: " + filePath);
    }
}