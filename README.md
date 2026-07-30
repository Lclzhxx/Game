# 秘境·凡尘（mijing-fanchen）

> 2.5D 国风修仙 RPG —— 《秘境·凡尘》生产工程（Sprint 1 地基）。
> 引擎：**Unity 2022.3.62 LTS**（中国区 `2022.3.62f3c1`，API 等价国际 `f1`），
> 渲染管线：**URP 14.0.6**（已钉死，禁止升级到 v17 / Unity 6）。

## 一句话说明
本仓库是游戏的 Unity 工程（非工作区文档）。当前处于 **S1 地基阶段**：仅含工程脚手架、
墨韵（Ink）渲染栈加固、灰盒工具与 CI 骨架，**不含任何游戏业务玩法代码**。

## 如何打开
1. 安装 **Unity 2022.3.62 LTS**（中国区 `2022.3.62f3c1` 亦可，API 等价）。
2. Unity Hub → "项目" → "打开" → 选中本仓库根目录。
3. 等待编译完成（底部 Compiling 消失），Console **无红色 Error** 即成功。
4. 菜单 `Greybox → Rebuild Scene` 一键搭灰盒；点 ▶ 运行验证 H1/H2。

## 墨韵（Ink）栈
- `Assets/Scripts/Rendering/InkRenderFeature.cs` —— 单条全屏 Pass（ADR-002，单路径、无 RTHandle/RenderGraph，C5 跨版本安全）。
- `Assets/Shaders/InkFullscreen.shader` —— 程序化宣纸/墨线/飞白/墨渍，零外部贴图。
- 首次打开后：`Greybox → Create Ink Material` 生成 `Assets/Materials/InkMaterial.mat`，
  再在 URP Renderer 资产里 `Add Renderer Feature → InkRenderFeature` 并赋上该材质。
  （也可点 `MJ → Setup URP + Ink` 一键完成上述 URP 资产 + 墨韵接线。）

## 目录规范（S1 落地）
```
Assets/
  Scripts/
    Rendering/   InkRenderFeature.cs        (R 渲染·墨韵)
    Camera/      CameraRig.cs               (R 固定斜45°相机)
    Core/        FpsProbe.cs, GreyboxBuilder.cs (I 遥测/灰盒)
    Editor/      GreyboxMenu.cs, InkMaterialCreator.cs, UrpBootstrap.cs (I 工具)
    Player/      PlayerController.cs        (C 灰盒临时映射，待重构)
    Enemy/       DummyEnemy.cs              (C 灰盒占位，待重构)
  Shaders/       InkFullscreen.shader
  Art/ Audio/ Resources/ Settings/          (后续资产/配置目录，先占位)
Packages/        manifest.json  (URP 14.0.6 钉死)
ProjectSettings/
.github/workflows/ci.yml  (自托管 GameCI 构建 + 门禁骨架)
```

## CI 状态 / 如何下载试玩构建
- 自托管 GitHub Actions runner（`runs-on: self-hosted`）监听 `push/PR → main`，
  用 GameCI `unity-builder` 构建 Windows 可执行，产出 Artifact `game-build`。
- 门禁（骨架，待 E1-S1 接实）：墨韵回归 + 帧率冒烟（FPS ≥ 58）。
- 下载：仓库 `Actions` → 对应 run → Artifacts → `game-build` → 解压双击 `.exe`。

> 注意：本机构建机仅 **8GB 内存**；CI 已按无头（`-batchmode -nographics`）设计，
> 并建议把 Windows 页面文件放大到 16–24GB（置于 D 盘）以补偿物理内存。详见 `ci.yml` 注释。
