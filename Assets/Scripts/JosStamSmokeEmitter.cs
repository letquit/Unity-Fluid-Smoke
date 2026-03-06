using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 基于Jos Stam算法实现的烟雾模拟发射器组件
/// 使用流体动力学方程模拟真实的烟雾运动效果
/// </summary>
public class JosStamSmokeEmitter : MonoBehaviour
{
    [Header("Grid Settings")]
    // 网格分辨率（默认64×64）
    public int GridSize = 64;
    public float CellSize = 0.1f;

    [Header("Fluid Properties")]
    [SerializeField] private float _viscosity = 0.0001f;
    [SerializeField] private float _diffusion = 0.0001f;
    [SerializeField] private float _timeStep = 0.0167f;

    [Header("Smoke Properties")]
    [SerializeField] private float _smokeDecay = 0.995f;
    [SerializeField] private float _emissionStrength = 150f;
    [SerializeField] private Vector2 _emissionPoint = new Vector2(32, 10);
    [SerializeField] private float _emissionRadius = 5f;
    [SerializeField] private Vector2 _emissionVelocity = new Vector2(0f, 20f);
    [SerializeField] private float _velocityRandomness = 50f;

    [Header("Visualization")]
    [SerializeField] private Material _smokeMaterial;
    [SerializeField] private int _sortingOrder = 1000;
    [SerializeField] private Sprite _sprite;

    // 流体网格数组：密度和速度场数据
    // 烟雾密度场（当前/上一帧）
    private float[,] _density, _prevDensity;
    // X方向速度场
    private float[,] _velocityX, _prevVelocityX;
    // Y方向速度场
    private float[,] _velocityY, _prevVelocityY;

    // 可视化相关对象
    // 渲染用的纹理
    private Texture2D _smokeTexture;
    private GameObject _smokeSpriteObj;

    private float _emissionRadiusSquared;

    private int _smokeTextureProperty = Shader.PropertyToID("_SmokeTexture");

    /// <summary>
    /// 初始化组件，设置发射半径平方值、流体数组和可视化对象
    /// </summary>
    private void Start()
    {
        _emissionRadiusSquared = _emissionRadius * _emissionRadius;
        InitializeFluidArrays();
        CreateVisualization();
    }

    /// <summary>
    /// 初始化流体模拟所需的二维数组
    /// 创建密度场和速度场的当前值与前一帧值数组
    /// </summary>
    private void InitializeFluidArrays()
    {
        int size = GridSize + 2;
        
        _density = new float[size, size];
        _prevDensity = new float[size, size];
        _velocityX = new float[size, size];
        _prevVelocityX = new float[size, size];
        _velocityY = new float[size, size];
        _prevVelocityY = new float[size, size];
    }

    /// <summary>
    /// 创建用于显示烟雾的纹理和精灵渲染器
    /// 设置渲染材质和纹理属性
    /// </summary>
    private void CreateVisualization()
    {
        _smokeTexture = new Texture2D(GridSize, GridSize, TextureFormat.RGBA32, false);
        _smokeTexture.filterMode = FilterMode.Bilinear;

        _smokeSpriteObj = new GameObject("SmokeDisplay");
        _smokeSpriteObj.transform.parent = transform;
        _smokeSpriteObj.transform.localPosition = Vector3.zero;
        _smokeSpriteObj.transform.localScale = new Vector3(GridSize * CellSize, GridSize * CellSize, 1f);

        SpriteRenderer sr = _smokeSpriteObj.AddComponent<SpriteRenderer>();

        sr.material = new Material(_smokeMaterial);
        sr.material.SetTexture(_smokeTextureProperty, _smokeTexture);
        sr.sprite = _sprite;

        sr.sortingOrder = _sortingOrder;
    }
    
    /// <summary>
    /// 每帧更新烟雾模拟
    /// 添加烟雾源、执行速度步进、密度步进并更新纹理显示
    /// </summary>
    private void Update()
    {
        AddSmokeSource();
        VelocityStep();
        DensityStep();
        UpdateSmokeTexture();
    }

    /// <summary>
    /// 在指定位置添加烟雾源
    /// 根据发射点、半径和强度向流体网格中添加密度和速度
    /// </summary>
    private void AddSmokeSource()
    {
        int centerX = Mathf.RoundToInt(_emissionPoint.x);
        int centerY = Mathf.RoundToInt(_emissionPoint.y);
        int radius = Mathf.RoundToInt(_emissionRadius);

        // 圆形发射区域
        for (int i = -radius; i <= radius; i++)
        {
            for (int j = -radius; j <= radius; j++)
            {
                float distanceSquared = i * i + j * j;
                if (distanceSquared > _emissionRadiusSquared) continue;

                int x = centerX + i;
                int y = centerY + j;

                if (x >= 1 && x <= GridSize && y >= 1 && y <= GridSize)
                {
                    float distance = Mathf.Sqrt(distanceSquared);
                    // 密度衰减（中心强，边缘弱）
                    float falloff = 1f - (distance / _emissionRadius);
                    _density[x, y] += _emissionStrength * falloff * _timeStep;

                    float randomVelX = Random.Range(-_velocityRandomness, _velocityRandomness);
                    float randomVelY = Random.Range(-_velocityRandomness, _velocityRandomness);

                    // 速度 + 随机扰动
                    _velocityX[x, y] += (_emissionVelocity.x + randomVelX) * falloff * _timeStep;
                    _velocityY[x, y] += (_emissionVelocity.y + randomVelY) * falloff * _timeStep;
                }
            }
        }
    }

    /// <summary>
    /// 执行流体速度场的时间步进
    /// 包括扩散、对流和投影步骤以保持流体不可压缩性
    /// </summary>
    private void VelocityStep()
    {
        System.Array.Copy(_velocityX, _prevVelocityX, _velocityX.Length);
        System.Array.Copy(_velocityY, _prevVelocityY, _velocityY.Length);

        // 步骤1: 扩散 - 粘度影响
        Diffuse(1, _velocityX, _prevVelocityX, _viscosity, _timeStep);
        Diffuse(2, _velocityY, _prevVelocityY, _viscosity, _timeStep);

        System.Array.Copy(_velocityX, _prevVelocityX, _velocityX.Length);
        System.Array.Copy(_velocityY, _prevVelocityY, _velocityY.Length);

        // 步骤2: 对流 - 速度自传输
        Advect(1, _velocityX, _prevVelocityX, _prevVelocityX, _prevVelocityY, _timeStep);
        Advect(2, _velocityY, _prevVelocityY, _prevVelocityX, _prevVelocityY, _timeStep);

        // 步骤3: 投影 - 保持质量守恒（不可压缩）
        Project(_velocityX, _velocityY, _prevVelocityX, _prevVelocityY);
    }

    /// <summary>
    /// 执行流体密度场的时间步进
    /// 包括扩散、对流和衰减处理
    /// </summary>
    private void DensityStep()
    {
        System.Array.Copy(_density, _prevDensity, _density.Length);
        // 步骤1: 扩散 - 烟雾自然散开
        Diffuse(0, _density, _prevDensity, _diffusion, _timeStep);

        System.Array.Copy(_density, _prevDensity, _density.Length);
        // 步骤2: 对流 - 随速度场移动
        Advect(0, _density, _prevDensity, _velocityX, _velocityY, _timeStep);

        // 应用烟雾衰减效果
        // 步骤3: 衰减 - 烟雾逐渐消失
        for (int i = 1; i <= GridSize; i++)
        {
            for (int j = 1; j <= GridSize; j++)
            {
                _density[i, j] *= _smokeDecay;  // 默认0.995
            }
        }
    }

    /// <summary>
    /// 执行扩散步骤
    /// 模拟流体中的粘性和扩散效应
    /// </summary>
    /// <param name="b">边界类型标识</param>
    /// <param name="x">目标数组</param>
    /// <param name="x0">源数组</param>
    /// <param name="diff">扩散系数</param>
    /// <param name="dt">时间步长</param>
    private void Diffuse(int b, float[,] x, float[,] x0, float diff, float dt)
    {
        // 模拟粘度/扩散效应，使用 高斯-赛德尔迭代法 求解
        float a = dt * diff * GridSize * GridSize;
        LinearSolve(b, x, x0, a, 1 + 4 * a);
    }

    /// <summary>
    /// 执行对流步骤
    /// 根据速度场移动密度或速度值
    /// </summary>
    /// <param name="b">边界类型标识</param>
    /// <param name="d">目标数组</param>
    /// <param name="d0">源数组</param>
    /// <param name="u">X方向速度场</param>
    /// <param name="v">Y方向速度场</param>
    /// <param name="dt">时间步长</param>
    private void Advect(int b, float[,] d, float[,] d0, float[,] u, float[,] v, float dt)
    {
        // 反向追踪法：从当前位置回溯到上一帧的位置采样
        float dt0 = dt * GridSize;

        for (int i = 1; i <= GridSize; i++)
        {
            for (int j = 1; j <= GridSize; j++)
            {
                // 核心公式：x_backtrace = x_current - dt * velocity
                float x = i - dt0 * u[i, j];
                float y = j - dt0 * v[i, j];

                x = Mathf.Clamp(x, 0.5f, GridSize + 0.5f);
                y = Mathf.Clamp(y, 0.5f, GridSize + 0.5f);

                int i0 = (int)x;
                int i1 = i0 + 1;
                int j0 = (int)y;
                int j1 = j0 + 1;

                float s1 = x - i0;
                float s0 = 1 - s1;
                float t1 = y - j0;
                float t0 = 1 - t1;

                // 双线性插值采样
                d[i, j] = s0 * (t0 * d0[i0, j0] + t1 * d0[i0, j1]) +
                          s1 * (t0 * d0[i1, j0] + t1 * d0[i1, j1]);
            }
        }

        SetBoundary(b, d);
    }

    /// <summary>
    /// 执行投影步骤
    /// 通过求解泊松方程来确保速度场的无散度特性（不可压缩性）
    /// </summary>
    /// <param name="u">X方向速度场</param>
    /// <param name="v">Y方向速度场</param>
    /// <param name="p">压力场</param>
    /// <param name="div">散度场</param>
    private void Project(float[,] u, float[,] v, float[,] p, float[,] div)
    {
        // 确保速度场无散度（不可压缩流体
        for (int i = 1; i <= GridSize; i++)
        {
            for (int j = 1; j <= GridSize; j++)
            {
                // 1. 计算散度
                div[i, j] = -0.5f * (u[i + 1, j] - u[i - 1, j] + v[i, j + 1] - v[i, j - 1]) / GridSize;
                p[i, j] = 0;
            }
        }

        SetBoundary(0, div);
        SetBoundary(0, p);
        // 2. 求解压强泊松方程
        LinearSolve(0, p, div, 1, 4);

        for (int i = 1; i <= GridSize; i++)
        {
            for (int j = 1; j <= GridSize; j++)
            {
                // 3. 从速度场减去压强梯度
                u[i, j] -= 0.5f * GridSize * (p[i + 1, j] - p[i - 1, j]);
                v[i, j] -= 0.5f * GridSize * (p[i, j + 1] - p[i, j - 1]);
            }
        }

        SetBoundary(1, u);
        SetBoundary(2, v);
    }

    /// <summary>
    /// 线性求解器
    /// 使用迭代方法求解扩散方程或泊松方程
    /// </summary>
    /// <param name="b">边界类型标识</param>
    /// <param name="x">目标数组</param>
    /// <param name="x0">源数组</param>
    /// <param name="a">系数a</param>
    /// <param name="c">系数c</param>
    private void LinearSolve(int b, float[,] x, float[,] x0, float a, float c)
    {
        for (int k = 0; k < 20; k++)
        {
            for (int i = 1; i <= GridSize; i++)
            {
                for (int j = 1; j <= GridSize; j++)
                {
                    x[i, j] = (x0[i, j] + a * (x[i - 1, j] + x[i + 1, j] + x[i, j - 1] + x[i, j + 1])) / c;
                }
            }
            SetBoundary(b, x);
        }
    }
    
    /// <summary>
    /// 设置边界条件
    /// 根据边界类型设置网格边缘的值（反射或零梯度）
    /// </summary>
    /// <param name="b">边界类型：0=标量场，1=X速度分量，2=Y速度分量</param>
    /// <param name="x">要设置边界的数组</param>
    private void SetBoundary(int b, float[,] x)
    {
        // 处理四边边界
        for (int i = 1; i <= GridSize; i++)
        {
            // 速度在垂直边界方向需要取反（反射边界条件）
            x[0, i] = b == 1 ? -x[1, i] : x[1, i];           // 左边界
            x[GridSize+1, i] = b == 1 ? -x[GridSize, i] : x[GridSize, i];  // 右边界
            x[i, 0] = b == 2 ? -x[i, 1] : x[i, 1];           // 下边界
            x[i, GridSize+1] = b == 2 ? -x[i, GridSize] : x[i, GridSize];  // 上边界
        }

        // 处理四个角落（平均值）
        x[0, 0] = 0.5f * (x[1, 0] + x[0, 1]);
        // 其他角落
        x[0, GridSize + 1] = 0.5f * (x[1, GridSize + 1] + x[0, GridSize]);
        x[GridSize + 1, 0] = 0.5f * (x[GridSize, 0] + x[GridSize + 1, 1]);
        x[GridSize + 1, GridSize + 1] = 0.5f * (x[GridSize, GridSize + 1] + x[GridSize + 1, GridSize]);
    }

    /// <summary>
    /// 更新烟雾纹理
    /// 将密度场数据转换为纹理像素颜色
    /// </summary>
    private void UpdateSmokeTexture()
    {
        Color[] colors = new Color[GridSize * GridSize];

        for (int i = 0; i < GridSize; i++)
        {
            for (int j = 0; j < GridSize; j++)
            {
                float densityValue = Mathf.Clamp01(_density[i + 1, j + 1] / 10f);
                colors[j * GridSize + i] = new Color(densityValue, densityValue, densityValue, densityValue);
            }
        }

        _smokeTexture.SetPixels(colors);
        _smokeTexture.Apply();
    }

    /// <summary>
    /// 设置阻挡单元格
    /// 将指定网格位置的密度和速度设为零，模拟障碍物
    /// </summary>
    /// <param name="x">网格X坐标</param>
    /// <param name="y">网格Y坐标</param>
    public void SetBlockedCell(int x, int y)
    {
        _density[x, y] = 0;
        
        _velocityX[x, y] = 0;
        _velocityY[x, y] = 0;
        
        _prevDensity[x, y] = 0;
        _prevVelocityX[x, y] = 0;
        _prevVelocityY[x, y] = 0;
    }

    /// <summary>
    /// 绘制Gizmos显示发射区域
    /// 在编辑器中可视化烟雾发射器的作用范围
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 emissionWorldPos = transform.position + new Vector3((_emissionPoint.x - GridSize * 0.5f) * CellSize,
            (_emissionPoint.y - GridSize * 0.5f) * CellSize, 0f);
        
        Gizmos.DrawWireSphere(emissionWorldPos, _emissionRadius * CellSize);
    }
}