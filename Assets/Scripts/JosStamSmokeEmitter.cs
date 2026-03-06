using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class JosStamSmokeEmitter : MonoBehaviour
{
    [Header("Grid Settings")]
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

    private float[,] _density, _prevDensity;
    private float[,] _velocityX, _prevVelocityX;
    private float[,] _velocityY, _prevVelocityY;

    private Texture2D _smokeTexture;
    private GameObject _smokeSpriteObj;

    private float _emissionRadiusSquared;

    private int _smokeTextureProperty = Shader.PropertyToID("_SmokeTexture");

    private void Start()
    {
        _emissionRadiusSquared = _emissionRadius * _emissionRadius;
        InitializeFluidArrays();
        CreateVisualization();
    }

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
    
    private void Update()
    {
        AddSmokeSource();
        VelocityStep();
        DensityStep();
        UpdateSmokeTexture();
    }

    private void AddSmokeSource()
    {
        int centerX = Mathf.RoundToInt(_emissionPoint.x);
        int centerY = Mathf.RoundToInt(_emissionPoint.y);
        int radius = Mathf.RoundToInt(_emissionRadius);

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
                    float falloff = 1f - (distance / _emissionRadius);
                    _density[x, y] += _emissionStrength * falloff * _timeStep;

                    float randomVelX = Random.Range(-_velocityRandomness, _velocityRandomness);
                    float randomVelY = Random.Range(-_velocityRandomness, _velocityRandomness);

                    _velocityX[x, y] += (_emissionVelocity.x + randomVelX) * falloff * _timeStep;
                    _velocityY[x, y] += (_emissionVelocity.y + randomVelY) * falloff * _timeStep;
                }
            }
        }
    }

    private void VelocityStep()
    {
        System.Array.Copy(_velocityX, _prevVelocityX, _velocityX.Length);
        System.Array.Copy(_velocityY, _prevVelocityY, _velocityY.Length);

        Diffuse(1, _velocityX, _prevVelocityX, _viscosity, _timeStep);
        Diffuse(2, _velocityY, _prevVelocityY, _viscosity, _timeStep);

        System.Array.Copy(_velocityX, _prevVelocityX, _velocityX.Length);
        System.Array.Copy(_velocityY, _prevVelocityY, _velocityY.Length);

        Advect(1, _velocityX, _prevVelocityX, _prevVelocityX, _prevVelocityY, _timeStep);
        Advect(2, _velocityY, _prevVelocityY, _prevVelocityX, _prevVelocityY, _timeStep);

        Project(_velocityX, _velocityY, _prevVelocityX, _prevVelocityY);
    }

    private void DensityStep()
    {
        System.Array.Copy(_density, _prevDensity, _density.Length);
        Diffuse(0, _density, _prevDensity, _diffusion, _timeStep);

        System.Array.Copy(_density, _prevDensity, _density.Length);
        Advect(0, _density, _prevDensity, _velocityX, _velocityY, _timeStep);

        for (int i = 1; i <= GridSize; i++)
        {
            for (int j = 1; j <= GridSize; j++)
            {
                _density[i, j] *= _smokeDecay;
            }
        }
    }

    private void Diffuse(int b, float[,] x, float[,] x0, float diff, float dt)
    {
        float a = dt * diff * GridSize * GridSize;
        LinearSolve(b, x, x0, a, 1 + 4 * a);
    }

    private void Advect(int b, float[,] d, float[,] d0, float[,] u, float[,] v, float dt)
    {
        float dt0 = dt * GridSize;

        for (int i = 1; i <= GridSize; i++)
        {
            for (int j = 1; j <= GridSize; j++)
            {
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

                d[i, j] = s0 * (t0 * d0[i0, j0] + t1 * d0[i0, j1]) +
                          s1 * (t0 * d0[i1, j0] + t1 * d0[i1, j1]);
            }
        }

        SetBoundary(b, d);
    }

    private void Project(float[,] u, float[,] v, float[,] p, float[,] div)
    {
        for (int i = 1; i <= GridSize; i++)
        {
            for (int j = 1; j <= GridSize; j++)
            {
                div[i, j] = -0.5f * (u[i + 1, j] - u[i - 1, j] + v[i, j + 1] - v[i, j - 1]) / GridSize;
                p[i, j] = 0;
            }
        }

        SetBoundary(0, div);
        SetBoundary(0, p);
        LinearSolve(0, p, div, 1, 4);

        for (int i = 1; i <= GridSize; i++)
        {
            for (int j = 1; j <= GridSize; j++)
            {
                u[i, j] -= 0.5f * GridSize * (p[i + 1, j] - p[i - 1, j]);
                v[i, j] -= 0.5f * GridSize * (p[i, j + 1] - p[i, j - 1]);
            }
        }

        SetBoundary(1, u);
        SetBoundary(2, v);
    }

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
    
    private void SetBoundary(int b, float[,] x)
    {
        for (int i = 1; i <= GridSize; i++)
        {
            x[0, i] = b == 1 ? -x[1, i] : x[1, i];
            x[GridSize + 1, i] = b == 1 ? -x[GridSize, i] : x[GridSize, i];
            x[i, 0] = b == 2 ? -x[i, 1] : x[i, 1];
            x[i, GridSize + 1] = b == 2 ? -x[i, GridSize] : x[i, GridSize];
        }

        x[0, 0] = 0.5f * (x[1, 0] + x[0, 1]);
        x[0, GridSize + 1] = 0.5f * (x[1, GridSize + 1] + x[0, GridSize]);
        x[GridSize + 1, 0] = 0.5f * (x[GridSize, 0] + x[GridSize + 1, 1]);
        x[GridSize + 1, GridSize + 1] = 0.5f * (x[GridSize, GridSize + 1] + x[GridSize + 1, GridSize]);
    }

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

    public void SetBlockedCell(int x, int y)
    {
        _density[x, y] = 0;
        
        _velocityX[x, y] = 0;
        _velocityY[x, y] = 0;
        
        _prevDensity[x, y] = 0;
        _prevVelocityX[x, y] = 0;
        _prevVelocityY[x, y] = 0;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 emissionWorldPos = transform.position + new Vector3((_emissionPoint.x - GridSize * 0.5f) * CellSize,
            (_emissionPoint.y - GridSize * 0.5f) * CellSize, 0f);
        
        Gizmos.DrawWireSphere(emissionWorldPos, _emissionRadius * CellSize);
    }
}
