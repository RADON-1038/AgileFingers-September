
/*
* Rastramento Básico de Mão com MediaPipe Hands com Retorno das Coordenadas com uso de ARFoundation *

- Utilizando uma ARCamera e retornando as coordenadas "hand_landmarks" (normalizadas) no console...
- Utilização de GPU para processamento, com configurações padrões da documentação oficial (TFFull).
- Processamento no modo ASSÍNCRONO para MediaPipe (obrigatório com ARFoundation): https://github.com/homuler/MediaPipeUnityPlugin/wiki/Advanced-Topics
- Processamento no modo SÍNCRONO para ARFoundation: https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@4.2/manual/cpu-camera-image.html

-- Info sobre o "hand_world_landmarks": https://github.com/google/mediapipe/issues/2199#issuecomment-1002299634

By Davi Neves => davimedio01 // 2022
*/

using System.Collections;
using System.Collections.Generic;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.CoordinateSystem; //Conversão de coordenadas do MediaPipe para Unity

using Stopwatch = System.Diagnostics.Stopwatch; //Timestamp baseado no tempo de execução da aplicação
using System;

public class MediaPipe_BasicHandTracking_ARFoundation : MonoBehaviour
{
    //Modos de Execução (CPU, GPU PC/Android) disponíveis para a aplicação
    private enum InferenceMode
    {
        CPU,
        GPU
    }
    //Tipos de Modelos TF disponíveis para a aplicação
    private enum ModelComplexity
    {
        Lite = 0,
        Full = 1,
    }

    //Apenas para enumerar o máximo de mãos permitidos nesse exemplo
    private enum MaxNumberHands
    {
        One = 1,
        Two = 2,
    }

    //Arquivo de configuração do grafo do modelo "Hands" ("hand_tracking_gpu.txt" para Linux ou "hand_tracking_opengles" para Android)
    [SerializeField] private TextAsset _cpuConfig;    //hand_tracking_cpu.txt
    [SerializeField] private TextAsset _gpuConfig;    //hand_tracking_gpu.txt
    [SerializeField] private TextAsset _openGlEsConfig; //hand_tracking_opengles.txt
    [SerializeField] private InferenceMode _inferenceMode = InferenceMode.GPU; //Modo de operação do grafo (CPU, GPU ou GPU Mobile)
    private TextAsset _configAsset; //Arquivo de configuração final baseado na escolha do usuario

    //Configuração personalizável do tipo de modelo a ser carregado
    [SerializeField] private ModelComplexity _modelComplexity = ModelComplexity.Full;

    //Configuração personalizável da quantidade de mãos a serem rastreadas na cena
    [SerializeField] private MaxNumberHands _maxNumHands = MaxNumberHands.One;

  //Hand Visualizer using sphere prefabs
  //[SerializeField] private NewHSV _handLandmarksVisualizer;
  [SerializeField] private HandSkeletonVisualize _handLandmarksVisualizer;

  //!!!!!!!!!!!!!!!!!! Apenas para testes
  // Controlador da estrutura que permite visualizar os pontos no Canvas desejado 
  //  [SerializeField] private MultiHandLandmarkListAnnotationController _handLandmarksAnnotationController;

  //Objeto do ARCameraManager relacionado ao uso do ARFoundation
  [SerializeField] private ARCameraManager _cameraManager;

    //[SerializeField] private Text outputText;
  // //Tamanho e FPS da câmera desejada! (Vai encontrar o valor igual ou próximo ao colocado aqui)
  // !!Necessário ver como fazer isso com uso da ARCamera (se é possível ou não...) => importante para testes
  // [SerializeField] private int _width = 1280;
  // [SerializeField] private int _height = 720;
  // [SerializeField] private int _fps = 30;


  //new 8.19-2
    public Text mediapipeText;
    //Grafo do MediaPipe
    private CalculatorGraph _graph;

    //Variável para conversão de coordenadas do MediaPipe para Unity
    private UnityEngine.Rect _screenRect;

    //Atribuir Timestamp com base no horário da máquina para os Packets do Grafo
    private Stopwatch _stopwatch;

    //Recursos para o Grafo do MediaPipe, isto é, os modelos em TF
    private ResourceManager _resourceManager;

    //Utilização de GPU para o Grafo
    private GpuResources _gpuResources;

    //Buffer do frame obtido da ARCamera
    private NativeArray<byte> _buffer;

    //Nome da Stream de Entrada do Grafo
    private const string _InputStreamName = "input_video";

    //Nome das Streams de Packets de Configuração do Grafo (Side Packets)
    private const string _SidePacketModelComplexity = "model_complexity";
    private const string _SidePacketMaxHandsName = "num_hands";

    //Abaixo, nomes associados com as Streams de SAÍDA possíveis do Grafo para solução Hands
    private const string _PalmDetectionsStreamName = "palm_detections";
    private const string _HandRectsFromPalmDetectionsStreamName = "hand_rects_from_palm_detections";
    private const string _HandLandmarkStreamName = "hand_landmarks";
    private const string _HandWorldLandmarkStreamName = "hand_world_landmarks";
    private const string _HandRectsFromLandmarksStreamName = "hand_rects_from_landmarks";
    private const string _HandednessStreamName = "handedness";

    //Variáveis para retorno da lista de posições das mãos: "hand_landmarks" e "hand_world_landmarks"
    private OutputStream<NormalizedLandmarkListVectorPacket, List<NormalizedLandmarkList>> _handLandmarkStream;

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //IEnumerator: https://pt.stackoverflow.com/questions/191582/o-que-%C3%A9-e-pra-que-serve-ienumerable-e-ienumerator
    //Basicamente, para ser possível prever que haja próximos dados (futuro...), como um iterador mesmo. Funciona como corotina.
    private IEnumerator Start()
    {
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //Configurações de Log e Debbuging do MediaPipe para o Console da Unity
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        Glog.Logtostderr = true;
        Glog.Minloglevel = 0;
        Glog.V = 3;
        //Glog.Initialize("MediaPipeUnityPlugin"); //Deve ser ativado apenas uma vez ao iniciar a Unity
        Protobuf.SetLogHandler(Protobuf.DefaultLogHandler);

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //Configuração da ARCamera e recuperação do frame atual
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //Função do ARFoundation que recupera o frame atual
        _cameraManager.frameReceived += OnCameraFrameReceived;

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //Configuração do MediaPipe API para Unity
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //Inicialização da GPU, caso esteja no modo GPU
        //_gpuResources = (_inferenceMode == InferenceMode.GPU) ? GpuResources.Create().Value() : null;
        _gpuResources = GpuResources.Create().Value();

        //Recuperando os modelos necessários para execução do Grafo "solução HANDS"
        _resourceManager = new StreamingAssetsResourceManager();
        if (_modelComplexity == ModelComplexity.Lite)
        {
            yield return _resourceManager.PrepareAssetAsync("hand_landmark_lite.bytes");
            yield return _resourceManager.PrepareAssetAsync("hand_recrop.bytes");
            yield return _resourceManager.PrepareAssetAsync("handedness.txt");
            yield return _resourceManager.PrepareAssetAsync("palm_detection_lite.bytes");
        }
        else
        {
            yield return _resourceManager.PrepareAssetAsync("hand_landmark_full.bytes");
            yield return _resourceManager.PrepareAssetAsync("hand_recrop.bytes");
            yield return _resourceManager.PrepareAssetAsync("handedness.txt");
            yield return _resourceManager.PrepareAssetAsync("palm_detection_full.bytes");
        }

        //Iniciando a variável de Timestamp (baseado no tempo de execução da aplicação)
        _stopwatch = new Stopwatch();

        //Iniciando o arquivo de configuração desejado
        //Mobile
        // if (SystemInfo.deviceType == DeviceType.Handheld)
        // {
        //     //OpenGLES para GPU Mobile
        //     _configAsset = _openGlEsConfig;
        //     Debug.Log("DeviceType: " + SystemInfo.deviceType);
        // }
        // //Desktop
        // else if (SystemInfo.deviceType == DeviceType.Desktop)
        // {
        //     //CPU
        //     if (_inferenceMode == InferenceMode.CPU)
        //     {
        //         _configAsset = _cpuConfig;
        //     }
        //     //GPU
        //     else if (_inferenceMode == InferenceMode.GPU)
        //     {
        //         _configAsset = _gpuConfig;
        //     }
        // }
        // //Caso seja desconhecido, impor Mobile
        // else
        // {
        //     //OpenGLES para GPU Mobile
        //     _configAsset = _openGlEsConfig;
        // }
        _configAsset = _gpuConfig;

        //Alocando o Grafo para a solução Hands
        _graph = new CalculatorGraph(_configAsset.text);
        if (_gpuResources != null)
            _graph.SetGpuResources(_gpuResources).AssertOk();

        //Iniciando as variáveis de saída do Grafo, com as configurações necessárias
        _handLandmarkStream = new OutputStream<NormalizedLandmarkListVectorPacket, List<NormalizedLandmarkList>>(
            _graph,
            _HandLandmarkStreamName
        );

        _handLandmarkStream.AddListener(OnHandLandmarksOutput);

        //Configurações de início das variáveís de saída dos resultados do MediaPipe (recebimento de valores)
        _handLandmarkStream.StartPolling().AssertOk();


        //Mandando as configurações personalizáveis (Side Packets) para o Grafo (ANTES DO INÍCIO!)
        var sidePacket = new SidePacket();
        sidePacket.Emplace(_SidePacketModelComplexity, new IntPacket((int)_modelComplexity));
        sidePacket.Emplace(_SidePacketMaxHandsName, new IntPacket((int)_maxNumHands));
        //Coordenadas convertidas do MediaPipe para o Sistema Unity.
        sidePacket.Emplace("input_rotation", new IntPacket(0));
        sidePacket.Emplace("input_horizontally_flipped", new BoolPacket(false));
        sidePacket.Emplace("input_vertically_flipped", new BoolPacket(true));

        //Acionando o Grafo com configurações personalizadas de início (Side Packet) e o Timer
        _graph.StartRun(sidePacket).AssertOk();
        _stopwatch.Start();
    }

    private unsafe void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        //Tenta adquirir o último frame copiado
        if (_cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            //Iniciação do buffer
            alocBuffer(image);

            //Variável que contém os parâmetros de conversão (pixels)
            var conversionParams = new XRCpuImage.ConversionParams(image, TextureFormat.RGBA32);

            //Recuperando o ponteiro de destino, ou seja, relacionado a memória alocada para o buffer do frame
            var ptr = (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(_buffer);

            //Conversão do frame adquirido para o buffer relacionado
            image.Convert(conversionParams, ptr, _buffer.Length);
            image.Dispose(); //Desalocando recursos após conversão
            
            //Recuperação da imagem convertida no buffer para manipular na Unity
            var imageFrame = new ImageFrame(ImageFormat.Types.Format.Srgba, image.width, image.height, 4 * image.width, _buffer);
      mediapipeText.text = $"w={image.width},\nh={image.height}";
            //Recuperando o Timestamp atual
            var currentTimestamp = _stopwatch.ElapsedTicks / (TimeSpan.TicksPerMillisecond / 1000);

            //Criando o Packet para aplicação do MediaPipe
            var imageFramePacket = new ImageFramePacket(imageFrame, new Timestamp(currentTimestamp));

            //Adicionado o Packet do frame para o grafo em execução
            _graph.AddPacketToInputStream(_InputStreamName, imageFramePacket).AssertOk();
        }
    }

    //Alocação do Buffer, exigido por "OnCameraFrameReceived"
    private void alocBuffer(XRCpuImage image)
    {
        //Recuperação do tamanho total da imagem em RGBA, isto é: largura * altura * RGBA(4)
        var length = image.width * image.height * 4;

        //Alocação do buffer de acordo com o tamanho da imagem
        if (_buffer == null || _buffer.Length != length)
        {
            _buffer = new NativeArray<byte>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }
    }

    //Callback para saída do MediaPipe no modo Assíncrono
    // https://github.com/homuler/MediaPipeUnityPlugin/wiki/Advanced-Topics#asynchronous-api
    [AOT.MonoPInvokeCallback(typeof(CalculatorGraph.NativePacketCallback))]
    private static IntPtr HandLandmarkCallback(IntPtr graphPtr, int streamId, IntPtr packetPtr)
    {
        try
        {
            //Recupera a saída do Grafo: um vetor de pontos normalizados
            using (var packet = new NormalizedLandmarkListVectorPacket(packetPtr, false))
            {
                //Recuperação do Packet no modo assíncrono
                if (!packet.IsEmpty()) // when `observeTimestampBounds` is `true`, output packet can be empty
                {
                    //!!!!!!! Arrumar aqui: após recuperar, executar as funções de mostrar coordenadas
                    var output = packet.Get();

                    Debug.Log("SAÍDA::::" + output);
                }
            }

            //Retorno de um ponteiro com status de resultado
            return Status.Ok().mpPtr;
        }
        catch (Exception e)
        {
            return Status.FailedPrecondition(e.ToString()).mpPtr;
        }
    }


    //hand_landmarks no Console
    IEnumerator ConsoleResult(List<NormalizedLandmarkList> hand_landmark)
    {
        //Verificando se existem pontos para serem mostrados no frame
        if (hand_landmark != null && hand_landmark.Count > 0)
        {
            //Retornando os 21 pontos de cada mão no frame
            foreach (var hand in hand_landmark)
            {
                //Mostrando as coordenadas do frame no Console da Unity
                //Ponto 0
                //var wrist = hand.Landmark[0];
                //Debug.Log($"hand_landmarks: Unity Coordinates: {_screenRect.GetPoint(wrist)} - Image Coordinates: {wrist}");

                //Ponto 1
                var thumb_cmc = hand.Landmark[1];
                Debug.Log($"Unity Coordinates: {_screenRect.GetPoint(thumb_cmc)}, Image Coordinates: {thumb_cmc}");

            }
            yield return new WaitForSeconds(.1f);
        }
    }

    private void OnDestroy()
    {
        //Retorna o evento do frame da ARCamera para o padrão
        _cameraManager.frameReceived -= OnCameraFrameReceived;

        //Fechando todas as entradas do Grafo
        var statusGraph = _graph.CloseAllPacketSources();
        if (!statusGraph.Ok())
        {
            Debug.Log($"Failed to close packet sources: {statusGraph}");
        }

        //Espera o processamento das entradas restantes
        statusGraph = _graph.WaitUntilDone();
        if (!statusGraph.Ok())
        {
            Debug.Log(statusGraph);
        }

        //Encerra, por fim, as estruturas do Grafo e libera recursos
        _graph.Dispose();

        if (_gpuResources != null)
            _gpuResources.Dispose(); //Apenas se utilizado estrutura de GPU...

        //Desalocando o Buffer
        _buffer.Dispose();
    }

    private void OnApplicationQuit()
    {
        //Restaurando as configurações de avisos de erros do Protobuf
        Protobuf.ResetLogHandler();
    }
    private void OnHandLandmarksOutput(object stream, OutputEventArgs<List<NormalizedLandmarkList>> eventArgs)
    {
        //_handLandmarksAnnotationController.DrawLater(eventArgs.value);
        // 获取Landmark坐标
        if (eventArgs.value != null)
        {
            Debug.Log("Hand is detected.");
            var handLandmarks = eventArgs.value;
            foreach (var normalizedLandmarkList in handLandmarks)
            {
                var landmarks = normalizedLandmarkList.Landmark;
                foreach (var landmark in landmarks)
                {
                    var pos = new Vector3(landmark.X, landmark.Y, landmark.Z);
                    //handLandmarkPositions.Add(pos);          
                }
            }
      //8.19-2
      //mediapipeText.text = "controller" + _handLandmarksVisualizer.mediaPipeController;
      //问题：无法读到_handLandmarksVisualizer.mediaPipeController,可能是由于初始化顺序导致？

        _handLandmarksVisualizer.DrawLater(eventArgs.value);
        //mediapipeText.text = "1";


            //_handLandmarksVisualizer.DrawLater(eventArgs.value);
            //Debug.Log("Drawing joints...");
        }
    }

}

