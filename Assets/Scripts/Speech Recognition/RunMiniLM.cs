using System;
using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;



public class RunMiniLM : MonoBehaviour
{
    public ModelAsset modelAsset;
    public TextAsset vocabAsset;
    const BackendType backend = BackendType.GPUCompute;

    private string gotoFridgeStr = "Go to fridge";
    private string gotoSofaStr = "Go to sofa";
    private string gotoTableStr = "Go to table";

    private Tensor<float>[] _embeddings = new Tensor<float>[3];

    //Special tokens
    const int START_TOKEN = 101;
    const int END_TOKEN = 102;

    //Store the vocabulary
    string[] tokens;

    const int FEATURES = 384; //size of feature space

    Worker engine, dotScore;

    void Start()
    {
        tokens = vocabAsset.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        engine = CreateMLModel();
        dotScore = CreateDotScoreModel();

        var gotoFridge_tokens = GetTokens(gotoFridgeStr);
        var gotoSofa_tokens = GetTokens(gotoSofaStr);
        var gotoTable_tokens = GetTokens(gotoTableStr);

        _embeddings[0] = GetEmbedding(gotoFridge_tokens);
        _embeddings[1] = GetEmbedding(gotoSofa_tokens);
        _embeddings[2] = GetEmbedding(gotoTable_tokens);

        //float score = GetDotScore(embedding1, embedding2);
        //Debug.Log("===> Similarity Score: " + score);
    }

    public float GetDotScore(int embeddingsIndex, Tensor<float> transcription_embedding)
    {
        if (embeddingsIndex >= _embeddings.Length || embeddingsIndex < 0) return 0;

        dotScore.Schedule(_embeddings[embeddingsIndex], transcription_embedding);
        var output = (dotScore.PeekOutput() as Tensor<float>).DownloadToNativeArray();
        return output[0];
    }

    public Tensor<float> GetEmbedding(List<int> tokenList)
    {
        int N = tokenList.Count;
        using var input_ids = new Tensor<int>(new TensorShape(1, N), tokenList.ToArray());
        using var token_type_ids = new Tensor<int>(new TensorShape(1, N), new int[N]);
        int[] mask = new int[N];
        for (int i = 0; i < mask.Length; i++)
        {
            mask[i] = 1;
        }
        using var attention_mask = new Tensor<int>(new TensorShape(1, N), mask);

        engine.Schedule(input_ids, attention_mask, token_type_ids);

        var output = engine.PeekOutput().ReadbackAndClone() as Tensor<float>;
        return output;
    }

    Worker CreateMLModel()
    {
        var model = ModelLoader.Load(modelAsset);
        var graph = new FunctionalGraph();
        var inputs = graph.AddInputs(model);
        var tokenEmbeddings = Functional.Forward(model, inputs)[0];
        var attention_mask = inputs[1];
        var output = MeanPooling(tokenEmbeddings, attention_mask);
        var modelWithMeanPooling = graph.Compile(output);

        return new Worker(modelWithMeanPooling, backend);
    }

    //Get average of token embeddings taking into account the attention mask
    FunctionalTensor MeanPooling(FunctionalTensor tokenEmbeddings, FunctionalTensor attentionMask)
    {
        var mask = attentionMask.Unsqueeze(-1).BroadcastTo(new[] { FEATURES }); //shape=(1,N,FEATURES)
        var A = Functional.ReduceSum(tokenEmbeddings * mask, 1); //shape=(1,FEATURES)
        var B = A / (Functional.ReduceSum(mask, 1) + 1e-9f); //shape=(1,FEATURES)
        var C = Functional.Sqrt(Functional.ReduceSum(Functional.Square(B), 1, true)); //shape=(1,FEATURES)
        return B / C; //shape=(1,FEATURES)
    }

    Worker CreateDotScoreModel()
    {
        var graph = new FunctionalGraph();
        var input1 = graph.AddInput<float>(new TensorShape(1, FEATURES));
        var input2 = graph.AddInput<float>(new TensorShape(1, FEATURES));
        var output = Functional.ReduceSum(input1 * input2, 1);
        var dotScoreModel = graph.Compile(output);
        return new Worker(dotScoreModel, backend);
    }

    public List<int> GetTokens(string text)
    {
        //split over whitespace
        string[] words = text.ToLower().Split(null);

        var ids = new List<int>
        {
            START_TOKEN
        };

        string s = "";

        foreach (var word in words)
        {
            int start = 0;
            for (int i = word.Length; i >= 0; i--)
            {
                string subword = start == 0 ? word.Substring(start, i) : "##" + word.Substring(start, i - start);
                int index = Array.IndexOf(tokens, subword);
                if (index >= 0)
                {
                    ids.Add(index);
                    s += subword + " ";
                    if (i == word.Length) break;
                    start = i;
                    i = word.Length + 1;
                }
            }
        }

        ids.Add(END_TOKEN);

        //Debug.Log("===> Tokenized sentence = " + s);

        return ids;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _embeddings.Length; i++)
        {
            _embeddings[i]?.Dispose();
        }

        dotScore?.Dispose();
        engine?.Dispose();
    }
}
