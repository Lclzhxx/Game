# 工程仓库结构规范（给工程侧用，非程序员无需阅读）

> 目的：让游戏工程（在 Unity 里那个项目）和本工作区的设计/文档资料保持清晰边界，
> 避免"代码、设计稿、美术资产"混在一起找不到。工程侧照此结构搭建仓库即可。

## 一、游戏工程仓库（Unity 项目，位于你电脑上的 Unity 工程目录）
```
YourGame/
├── Assets/
│   ├── Scripts/          # 所有 C# 代码（已有 prototype 的 Camera/Player/Enemy/Rendering/Core）
│   ├── Shaders/          # 着色器（已有 InkFullscreen.shader）
│   ├── Art/              # 美术资产（角色/敌人/场景/UI，按 art-bible 命名规范）
│   ├── Audio/            # 音频（后续 audio-director 接入）
│   └── Resources/        # 运行时加载的配置（GDD 里的数值表）
├── Packages/
│   └── manifest.json     # ⚠️ 关键：把 URP 钉死为 14.x（与 Unity 2022.3 匹配）
├── ProjectSettings/      # Unity 工程设置（公司名/产品名/图标等）
├── .github/workflows/    # 放 ci-workflow-template.yml（自动构建）
└── README.md             # 一句话说明这是啥项目 + 怎么打开
```

## 二、本工作区（D:\WBzone\Game，设计/文档/资料）
```
D:\WBzone\Game\
├── design/
│   ├── game-concept.md        # 概念草案（已批准）
│   ├── gdd/                   # 全量 GDD（14 系统 + 索引 + MVP 范围）
│   └── art-bible/             # 美术圣经 + 资产规格 + 可访问性
├── docs/architecture/         # 技术评估 / 灰盒计划 / 生产架构
├── production/
│   ├── roadmap/sprint-plan.md # 冲刺计划（20 Epic / 约 395 故事点 / 19 冲刺 ≈9.5 个月）
│   ├── ci/                    # 自动构建模板（本目录）
│   └── PHASE5_LAUNCH_PACKAGE.md  # 启动包总览
└── novel_work/                # 小说文本工程（IP 素材，已收尾）
```

## 三、两条铁律
1. **Unity 版本**：工程一律用 2022.3 LTS 打开，**禁止升级到 Unity 6**（会摧毁现有墨韵 Shader）。
2. **URP 版本**：`manifest.json` 里钉死 14.x，不要用被误升到 17 的版本（之前踩过的坑）。
