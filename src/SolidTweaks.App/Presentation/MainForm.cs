using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SolidTweaks.Core.Features;
using SolidTweaks.Features;
using SolidTweaks.Infrastructure.Diagnostics;
using SolidTweaks.Infrastructure.Persistence;
using SolidTweaks.Infrastructure.SolidWorks;
using SolidTweaks.Infrastructure.SystemInfo;
using SolidTweaks.Shared;

namespace SolidTweaks.Presentation
{
    internal sealed class MainForm : Form
    {
        private const string ChannelUrl = "https://www.youtube.com/@CG3D-ID";
        private const string RepositoryUrl = "https://github.com/cilasgimenez/Solid-Tweaks";
        private readonly SolidWorksService solidWorks;
        private readonly ITweak autoRotateSketch;
        private readonly ITweak spaceNormalTo;
        private readonly ConfigurableShortcutTweak fixSelectionShortcut;
        private readonly ConfigurableShortcutTweak constructionGeometryShortcut;
        private readonly ConfigurableShortcutTweak measureShortcut;
        private readonly ConfigurableShortcutTweak offsetEntitiesShortcut;
        private readonly ShortcutBindingMigration shortcutMigration;
        private readonly ITweak exportStep;
        private readonly ITweak exportSheetMetalDxf;
        private readonly SolidWorksAddinRegistrationService commandFeatureRegistration;
        private readonly SystemInfoService systemInfo;
        private readonly Label solidWorksValue;
        private readonly Label gpuValue;
        private readonly Label dotNetValue;
        private readonly LinkLabel languageLink;
        private readonly CheckBox autoRotateCheckBox;
        private readonly CheckBox spaceNormalToCheckBox;
        private readonly LinkLabel fixSelectionShortcutLink;
        private readonly LinkLabel constructionGeometryShortcutLink;
        private readonly LinkLabel measureShortcutLink;
        private readonly LinkLabel offsetEntitiesShortcutLink;
        private readonly CheckBox exportStepCheckBox;
        private readonly CheckBox exportSheetMetalDxfCheckBox;
        private readonly Label autoRotateRequirementLabel;
        private readonly Label spaceNormalToRequirementLabel;
        private readonly Label fixSelectionShortcutRequirementLabel;
        private readonly Label constructionGeometryShortcutRequirementLabel;
        private readonly Label measureShortcutRequirementLabel;
        private readonly Label offsetEntitiesShortcutRequirementLabel;
        private readonly Button reconnectButton;
        private readonly Button updateButton;
        private readonly System.Windows.Forms.Timer updateAttentionTimer;
        private readonly System.Windows.Forms.Timer solidWorksMonitorTimer;
        private readonly ToolStripStatusLabel statusLabel;
        private readonly ToolTip helpToolTip;
        private readonly Image helpIconImage;
        private readonly Image channelLogoImage;
        private readonly Image youtubeLogoImage;
        private readonly Image githubLogoImage;
        private bool syncingUi;
        private bool updateInProgress;
        private bool connectionRefreshInProgress;
        private int updateAttentionStep;
        private bool? lastObservedHasActiveDocument;
        private readonly Dictionary<Control, string> localizedTexts = new Dictionary<Control, string>();
        private readonly Dictionary<Control, string> localizedAccessibleNames = new Dictionary<Control, string>();
        private readonly Dictionary<Control, string> localizedToolTips = new Dictionary<Control, string>();
        private readonly List<KeyValuePair<Control, Control>> helpIconBindings =
            new List<KeyValuePair<Control, Control>>();
        private readonly List<KeyValuePair<Control, Label>> requirementPositionBindings =
            new List<KeyValuePair<Control, Label>>();
        private readonly List<KeyValuePair<Control, Control>> helpAccessibleNameBindings =
            new List<KeyValuePair<Control, Control>>();

        public MainForm()
        {
            ApplicationLog.Info("Building the main user interface.");
            solidWorks = new SolidWorksService(new SolidWorksPreferenceResolver());
            autoRotateSketch = new AutoRotateSketchTweak(solidWorks);
            spaceNormalTo = new SpaceNormalToTweak(
                new SolidWorksKeyboardShortcutService(solidWorks),
                new FeatureStateStore(),
                ReportKeyboardProgress);
            commandFeatureRegistration = new SolidWorksAddinRegistrationService(solidWorks);
            var configurableKeyboardService = new SolidWorksKeyboardShortcutService(solidWorks);
            fixSelectionShortcut = new ConfigurableShortcutTweak(
                ShortcutAction.MakeFixed, configurableKeyboardService, ReportKeyboardProgress);
            constructionGeometryShortcut = new ConfigurableShortcutTweak(
                ShortcutAction.ConstructionGeometry, configurableKeyboardService, ReportKeyboardProgress);
            measureShortcut = new ConfigurableShortcutTweak(
                ShortcutAction.Measure, configurableKeyboardService, ReportKeyboardProgress);
            offsetEntitiesShortcut = new ConfigurableShortcutTweak(
                ShortcutAction.OffsetEntities, configurableKeyboardService, ReportKeyboardProgress);
            shortcutMigration = new ShortcutBindingMigration(
                configurableKeyboardService, new FeatureStateStore(), ReportKeyboardProgress);
            exportStep = new ExportStepTweak(commandFeatureRegistration);
            exportSheetMetalDxf = new ExportSheetMetalDxfTweak(commandFeatureRegistration);
            systemInfo = new SystemInfoService();
            helpIconImage = LoadHelpIcon();
            channelLogoImage = LoadEmbeddedImage("SolidTweaks.Brand.Logo_CG3D.png");
            youtubeLogoImage = LoadEmbeddedImage("SolidTweaks.Brand.Logo_Youtube.png");
            githubLogoImage = LoadEmbeddedImage("SolidTweaks.Brand.Logo_Github.png");
            helpToolTip = new ToolTip
            {
                IsBalloon = true,
                ShowAlways = true,
                ToolTipIcon = ToolTipIcon.Info,
                ToolTipTitle = UiText.Get("Help"),
                InitialDelay = 250,
                ReshowDelay = 100,
                AutoPopDelay = 12000
            };

            Text = "Solid Tweaks";
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(760, 660);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = UiPalette.Window;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            var header = BuildHeader(
                helpToolTip,
                channelLogoImage,
                youtubeLogoImage,
                githubLogoImage,
                OpenChannelOnClick,
                OpenRepositoryOnClick);
            var infoCard = BuildInfoCard(
                out solidWorksValue,
                out gpuValue,
                out dotNetValue,
                out languageLink,
                out reconnectButton);
            var tweaksCard = BuildTweaksCard(
                helpToolTip,
                helpIconImage,
                out autoRotateCheckBox,
                out spaceNormalToCheckBox,
                out fixSelectionShortcutLink,
                out constructionGeometryShortcutLink,
                out measureShortcutLink,
                out offsetEntitiesShortcutLink,
                out autoRotateRequirementLabel,
                out spaceNormalToRequirementLabel,
                out fixSelectionShortcutRequirementLabel,
                out constructionGeometryShortcutRequirementLabel,
                out measureShortcutRequirementLabel,
                out offsetEntitiesShortcutRequirementLabel);
            var resourcesCard = BuildResourcesCard(
                helpToolTip,
                helpIconImage,
                out exportStepCheckBox,
                out exportSheetMetalDxfCheckBox,
                out updateButton);
            updateAttentionTimer = new System.Windows.Forms.Timer { Interval = 75 };
            updateAttentionTimer.Tick += UpdateAttentionTimerOnTick;
            solidWorksMonitorTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            solidWorksMonitorTimer.Tick += SolidWorksMonitorTimerOnTick;
            StatusStrip statusStrip = BuildStatusStrip(out statusLabel);

            var content = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = UiPalette.Window,
                Padding = new Padding(24, 10, 24, 10)
            };
            Action alignCards = delegate
            {
                int width = Math.Max(
                    100,
                    content.ClientSize.Width - content.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                foreach (Control card in content.Controls)
                {
                    card.Width = width;
                }
            };
            content.SizeChanged += delegate { alignCards(); };
            content.ControlAdded += delegate { alignCards(); };
            content.Controls.Add(infoCard);
            content.Controls.Add(tweaksCard);
            content.Controls.Add(resourcesCard);

            Controls.Add(content);
            Controls.Add(header);
            Controls.Add(statusStrip);

            reconnectButton.Click += async delegate { await RefreshConnectionAsync(); };
            languageLink.LinkClicked += LanguageLinkOnLinkClicked;
            autoRotateCheckBox.CheckedChanged += AutoRotateCheckBoxOnCheckedChanged;
            spaceNormalToCheckBox.CheckedChanged += SpaceNormalToCheckBoxOnCheckedChanged;
            fixSelectionShortcutLink.LinkClicked += delegate { ConfigureShortcut(fixSelectionShortcut, fixSelectionShortcutLink); };
            constructionGeometryShortcutLink.LinkClicked += delegate { ConfigureShortcut(constructionGeometryShortcut, constructionGeometryShortcutLink); };
            measureShortcutLink.LinkClicked += delegate { ConfigureShortcut(measureShortcut, measureShortcutLink); };
            offsetEntitiesShortcutLink.LinkClicked += delegate { ConfigureShortcut(offsetEntitiesShortcut, offsetEntitiesShortcutLink); };
            exportStepCheckBox.CheckedChanged += ExportStepCheckBoxOnCheckedChanged;
            exportSheetMetalDxfCheckBox.CheckedChanged += ExportSheetMetalDxfCheckBoxOnCheckedChanged;
            updateButton.Click += UpdateButtonOnClick;
            statusLabel.Click += StatusLabelOnClick;
            Shown += async delegate
            {
                await InitializeAsync();
                solidWorksMonitorTimer.Start();
            };
            FormClosed += delegate
            {
                solidWorksMonitorTimer.Stop();
                solidWorksMonitorTimer.Dispose();
                updateAttentionTimer.Stop();
                updateAttentionTimer.Dispose();
                helpToolTip.Dispose();
                helpIconImage.Dispose();
                channelLogoImage.Dispose();
                youtubeLogoImage.Dispose();
                githubLogoImage.Dispose();
                solidWorks.Dispose();
            };
        }

        private Panel BuildHeader(
            ToolTip toolTip,
            Image channelLogo,
            Image youtubeLogo,
            Image githubLogo,
            EventHandler openChannel,
            EventHandler openRepository)
        {
            var panel = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = UiPalette.Surface };
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(UiPalette.Border))
                {
                    e.Graphics.DrawLine(pen, 0, panel.Height - 1, panel.Width, panel.Height - 1);
                }
            };

            panel.Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(24, 11),
                Text = "Solid Tweaks",
                Font = new Font("Segoe UI Semibold", 19F),
                ForeColor = UiPalette.Text
            });
            panel.Controls.Add(TrackText(new Label
            {
                AutoSize = true,
                Location = new Point(26, 48),
                Text = UiText.Get("Quick tweaks for SOLIDWORKS"),
                ForeColor = UiPalette.Muted
            }, "Quick tweaks for SOLIDWORKS"));

            var channelCard = new Panel
            {
                Size = new Size(170, 64),
                Location = new Point(30, 6),
                Anchor = AnchorStyles.Top,
                BackColor = UiPalette.Window,
                Cursor = Cursors.Hand,
                AccessibleName = UiText.Get("Open the CG3D channel on YouTube"),
                TabStop = true
            };
            Action alignChannelCard = delegate
            {
                int contentRightMargin = 24 + SystemInformation.VerticalScrollBarWidth;
                channelCard.Left = Math.Max(
                    420,
                    panel.ClientSize.Width - channelCard.Width - contentRightMargin);
            };
            channelCard.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(UiPalette.Border))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, channelCard.Width - 1, channelCard.Height - 1);
                }
            };

            var channelPicture = new PictureBox
            {
                Location = new Point(8, 7),
                Size = new Size(50, 50),
                BackColor = Color.Transparent,
                Image = channelLogo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand,
                AccessibleName = UiText.Get("CG3D channel logo")
            };
            var channelCaption = new Label
            {
                AutoSize = false,
                Location = new Point(62, 7),
                Size = new Size(104, 18),
                Text = UiText.Get("Follow on YouTube"),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 8F),
                ForeColor = UiPalette.Muted,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            TrackText(channelCaption, "Follow on YouTube");
            var youtubePicture = new PictureBox
            {
                Location = new Point(78, 32),
                Size = new Size(25, 18),
                BackColor = Color.Transparent,
                Image = youtubeLogo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand,
                AccessibleName = "YouTube"
            };
            var channelName = new Label
            {
                AutoSize = true,
                Location = new Point(109, 31),
                Text = "CG3D",
                Font = new Font("Segoe UI Semibold", 10F),
                ForeColor = UiPalette.Text,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                AccessibleName = UiText.Get("CG3D channel")
            };
            TrackAccessibleName(channelCard, "Open the CG3D channel on YouTube");
            TrackAccessibleName(channelPicture, "CG3D channel logo");
            TrackAccessibleName(channelName, "CG3D channel");

            channelCard.Controls.Add(channelPicture);
            channelCard.Controls.Add(channelCaption);
            channelCard.Controls.Add(youtubePicture);
            channelCard.Controls.Add(channelName);
            WireChannelLink(channelCard, channelCard, openChannel);
            TrackToolTip(channelCard, "Open the CG3D channel on YouTube");
            TrackToolTip(channelPicture, "Open the CG3D channel on YouTube");
            TrackToolTip(youtubePicture, "Open the CG3D channel on YouTube");
            TrackToolTip(channelName, "Open the CG3D channel on YouTube");

            var githubPicture = new PictureBox
            {
                Size = new Size(50, 50),
                BackColor = Color.Transparent,
                Image = githubLogo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand,
                AccessibleName = UiText.Get("Open the SolidTweaks project on GitHub"),
                TabStop = true
            };
            TrackAccessibleName(githubPicture, "Open the SolidTweaks project on GitHub");
            Action alignHeaderLinks = delegate
            {
                alignChannelCard();
                githubPicture.Location = new Point(
                    channelCard.Left - githubPicture.Width - 12,
                    channelCard.Top + (channelCard.Height - githubPicture.Height) / 2);
            };
            githubPicture.Click += openRepository;
            TrackToolTip(githubPicture, "Open the SolidTweaks project on GitHub");

            panel.Controls.Add(channelCard);
            panel.Controls.Add(githubPicture);
            panel.Resize += delegate { alignHeaderLinks(); };
            alignHeaderLinks();
            return panel;
        }

        private static void WireChannelLink(Control control, Panel card, EventHandler openChannel)
        {
            control.Click += openChannel;
            control.MouseEnter += delegate { card.BackColor = Color.FromArgb(242, 247, 253); };
            control.MouseLeave += delegate
            {
                Point cursor = card.PointToClient(Cursor.Position);
                if (!card.ClientRectangle.Contains(cursor))
                {
                    card.BackColor = UiPalette.Window;
                }
            };
            foreach (Control child in control.Controls)
            {
                WireChannelLink(child, card, openChannel);
            }
        }

        private Panel BuildInfoCard(
            out Label swValue,
            out Label graphicsValue,
            out Label frameworkValue,
            out LinkLabel language,
            out Button reconnect)
        {
            var card = CreateCard(142);
            card.Controls.Add(TrackText(CreateSectionTitle(UiText.Get("ENVIRONMENT"), 10), "ENVIRONMENT"));

            Label versionLabel = CreateValueLabel(UiText.Get("Getting version..."), 34);
            versionLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            swValue = versionLabel;
            graphicsValue = CreateValueLabel(UiText.Get("Getting graphics card..."), 58);
            frameworkValue = CreateValueLabel(UiText.Get("Getting .NET Framework..."), 82);
            card.Controls.Add(CreateCaptionLabel("SOLIDWORKS", 34));
            card.Controls.Add(swValue);
            card.Controls.Add(TrackText(CreateCaptionLabel(UiText.Get("GRAPHICS CARD"), 58), "GRAPHICS CARD"));
            card.Controls.Add(graphicsValue);
            card.Controls.Add(TrackText(CreateCaptionLabel(UiText.Get(".NET INSTALLED"), 82), ".NET INSTALLED"));
            card.Controls.Add(frameworkValue);

            card.Controls.Add(TrackText(CreateCaptionLabel(UiText.Get("LANGUAGE"), 106), "LANGUAGE"));
            language = new LinkLabel
            {
                AutoSize = true,
                Location = new Point(150, 106),
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = UiPalette.Accent,
                ActiveLinkColor = UiPalette.Accent,
                Cursor = Cursors.Hand,
                Text = CurrentLanguageDisplayName(),
                AccessibleName = UiText.Get("Change the Solid Tweaks language")
            };
            TrackAccessibleName(language, "Change the Solid Tweaks language");
            card.Controls.Add(language);

            Button reconnectButton = TrackText(CreateActionButton(UiText.Get("Reconnect"), card), "Reconnect");
            card.Resize += delegate
            {
                reconnectButton.Left = card.ClientSize.Width - reconnectButton.Width - 18;
                versionLabel.Width = Math.Max(80, reconnectButton.Left - versionLabel.Left - 12);
            };
            card.Controls.Add(reconnectButton);
            reconnect = reconnectButton;
            return card;
        }

        private Panel BuildTweaksCard(
            ToolTip toolTip,
            Image helpImage,
            out CheckBox autoRotateCheckBox,
            out CheckBox spaceNormalToCheckBox,
            out LinkLabel fixSelectionShortcutLink,
            out LinkLabel constructionGeometryShortcutLink,
            out LinkLabel measureShortcutLink,
            out LinkLabel offsetEntitiesShortcutLink,
            out Label autoRotateRequirementLabel,
            out Label spaceNormalToRequirementLabel,
            out Label fixSelectionShortcutRequirementLabel,
            out Label constructionGeometryShortcutRequirementLabel,
            out Label measureShortcutRequirementLabel,
            out Label offsetEntitiesShortcutRequirementLabel)
        {
            var card = CreateCard(252);
            card.Controls.Add(CreateSectionTitle("TWEAKS", 14));

            autoRotateCheckBox = new CheckBox
            {
                AutoSize = true,
                Enabled = false,
                Location = new Point(20, 42),
                Text = UiText.Get("Rotate when creating a sketch"),
                Font = new Font("Segoe UI Semibold", 11F),
                ForeColor = UiPalette.Text,
                Cursor = Cursors.Hand
            };
            TrackText(autoRotateCheckBox, "Rotate when creating a sketch");
            spaceNormalToCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(20, 76),
                Text = UiText.Get("Space key (3D View -> Normal To)"),
                Font = new Font("Segoe UI Semibold", 11F),
                ForeColor = UiPalette.Text,
                Cursor = Cursors.Hand
            };
            TrackText(spaceNormalToCheckBox, "Space key (3D View -> Normal To)");
            Control fixHelpIcon;
            Control constructionHelpIcon;
            Control measureHelpIcon;
            Control offsetHelpIcon;
            FlowLayoutPanel fixRow = CreateShortcutRow(
                "Make Fixed", 110, helpImage,
                out fixSelectionShortcutLink, out fixHelpIcon, out fixSelectionShortcutRequirementLabel);
            FlowLayoutPanel constructionRow = CreateShortcutRow(
                "Construction line", 144, helpImage,
                out constructionGeometryShortcutLink, out constructionHelpIcon, out constructionGeometryShortcutRequirementLabel);
            FlowLayoutPanel measureRow = CreateShortcutRow(
                "Measurement", 178, helpImage,
                out measureShortcutLink, out measureHelpIcon, out measureShortcutRequirementLabel);
            FlowLayoutPanel offsetRow = CreateShortcutRow(
                "Offset Entities", 212, helpImage,
                out offsetEntitiesShortcutLink, out offsetHelpIcon, out offsetEntitiesShortcutRequirementLabel);

            Control autoRotateHelpIcon = CreateHelpIcon(autoRotateCheckBox, 42, helpImage);
            Control spaceHelpIcon = CreateHelpIcon(spaceNormalToCheckBox, 76, helpImage);
            autoRotateRequirementLabel = CreateRequirementLabel(autoRotateHelpIcon, 42);
            spaceNormalToRequirementLabel = CreateRequirementLabel(spaceHelpIcon, 76);
            TrackToolTip(autoRotateHelpIcon, "Prevents the camera from automatically rotating normal to the plane when creating or editing a sketch.");
            TrackToolTip(spaceHelpIcon, "When enabled, Space runs Normal To for the selection.");
            TrackToolTip(fixHelpIcon, "Choose a keyboard shortcut to run Make Fixed on the selected sketch entity.");
            TrackToolTip(constructionHelpIcon, "Choose a keyboard shortcut to toggle construction geometry.");
            TrackToolTip(measureHelpIcon, "Choose a keyboard shortcut to open Measure.");
            TrackToolTip(offsetHelpIcon, "Choose a keyboard shortcut to open Offset Entities.");

            card.Controls.Add(autoRotateCheckBox);
            card.Controls.Add(autoRotateHelpIcon);
            card.Controls.Add(autoRotateRequirementLabel);
            card.Controls.Add(spaceNormalToCheckBox);
            card.Controls.Add(spaceHelpIcon);
            card.Controls.Add(spaceNormalToRequirementLabel);
            card.Controls.Add(fixRow);
            card.Controls.Add(constructionRow);
            card.Controls.Add(measureRow);
            card.Controls.Add(offsetRow);
            return card;
        }

        private FlowLayoutPanel CreateShortcutRow(
            string englishName,
            int top,
            Image helpImage,
            out LinkLabel shortcutLink,
            out Control helpIcon,
            out Label requirementLabel)
        {
            var row = new FlowLayoutPanel
            {
                Location = new Point(20, top),
                Size = new Size(650, 25),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            var nameLabel = new Label
            {
                AutoSize = true,
                Text = UiText.Get(englishName),
                Font = new Font("Segoe UI Semibold", 11F),
                ForeColor = UiPalette.Text,
                Margin = new Padding(0, 0, 0, 0)
            };
            TrackText(nameLabel, englishName);
            shortcutLink = new LinkLabel
            {
                AutoSize = true,
                Text = UiText.Get("Set key"),
                Font = new Font("Segoe UI Semibold", 10.5F),
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = UiPalette.Accent,
                ActiveLinkColor = UiPalette.Accent,
                Cursor = Cursors.Hand,
                Enabled = false,
                Margin = new Padding(8, 1, 0, 0)
            };
            helpIcon = new PictureBox
            {
                Size = new Size(19, 19),
                BackColor = Color.Transparent,
                Cursor = Cursors.Help,
                AccessibleName = UiText.Format("Help: {0}", UiText.Get(englishName)),
                TabStop = false,
                Image = helpImage,
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(8, 1, 0, 0)
            };
            helpAccessibleNameBindings.Add(new KeyValuePair<Control, Control>(nameLabel, helpIcon));
            requirementLabel = new Label
            {
                AutoSize = false,
                Size = new Size(220, 20),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = UiPalette.Error,
                BackColor = Color.Transparent,
                Visible = false,
                AccessibleName = UiText.Get("Requirement for enabling the feature"),
                Margin = new Padding(9, 2, 0, 0)
            };
            TrackAccessibleName(requirementLabel, "Requirement for enabling the feature");
            row.Controls.Add(nameLabel);
            row.Controls.Add(shortcutLink);
            row.Controls.Add(helpIcon);
            row.Controls.Add(requirementLabel);
            return row;
        }

        private Control CreateHelpIcon(Control relatedControl, int top, Image helpImage)
        {
            Size preferredSize = relatedControl.GetPreferredSize(Size.Empty);
            var icon = new PictureBox
            {
                Location = new Point(relatedControl.Left + preferredSize.Width + 8, top + 1),
                Size = new Size(19, 19),
                BackColor = Color.Transparent,
                Cursor = Cursors.Help,
                AccessibleName = UiText.Format("Help: {0}", relatedControl.Text),
                TabStop = false,
                Image = helpImage,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            helpIconBindings.Add(new KeyValuePair<Control, Control>(relatedControl, icon));
            helpAccessibleNameBindings.Add(new KeyValuePair<Control, Control>(relatedControl, icon));
            return icon;
        }

        private Label CreateRequirementLabel(Control helpIcon, int top)
        {
            var label = new Label
            {
                AutoSize = true,
                Location = new Point(helpIcon.Right + 9, top + 2),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = UiPalette.Error,
                BackColor = Color.Transparent,
                Visible = false,
                AccessibleName = UiText.Get("Requirement for enabling the feature")
            };
            TrackAccessibleName(label, "Requirement for enabling the feature");
            requirementPositionBindings.Add(new KeyValuePair<Control, Label>(helpIcon, label));
            return label;
        }

        private Panel BuildResourcesCard(
            ToolTip toolTip,
            Image helpImage,
            out CheckBox exportStepCheckBox,
            out CheckBox exportSheetMetalDxfCheckBox,
            out Button updateButton)
        {
            var card = CreateCard(116);
            card.Controls.Add(TrackText(CreateSectionTitle(UiText.Get("FEATURES"), 14), "FEATURES"));
            exportStepCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(20, 42),
                Text = UiText.Get("Export STEP"),
                Font = new Font("Segoe UI Semibold", 11F),
                ForeColor = UiPalette.Text,
                Cursor = Cursors.Hand
            };
            TrackText(exportStepCheckBox, "Export STEP");
            Control stepHelpIcon = CreateHelpIcon(exportStepCheckBox, 42, helpImage);
            TrackToolTip(stepHelpIcon, "Exports the part or assembly to STEP AP214 in the same folder, adding the time to the file name. Only visible bodies and components are included.");

            exportSheetMetalDxfCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(20, 76),
                Text = UiText.Get("Export Sheet Metal DXF"),
                Font = new Font("Segoe UI Semibold", 11F),
                ForeColor = UiPalette.Text,
                Cursor = Cursors.Hand
            };
            TrackText(exportSheetMetalDxfCheckBox, "Export Sheet Metal DXF");
            Control dxfHelpIcon = CreateHelpIcon(exportSheetMetalDxfCheckBox, 76, helpImage);
            TrackToolTip(dxfHelpIcon, "Adds a command to the Solid Tweaks tab for exporting sheet-metal flat patterns from parts and assemblies.");
            updateButton = TrackText(CreateActionButton(UiText.Get("Update"), card), "Update");
            updateButton.Visible = false;
            card.Controls.Add(exportStepCheckBox);
            card.Controls.Add(stepHelpIcon);
            card.Controls.Add(exportSheetMetalDxfCheckBox);
            card.Controls.Add(dxfHelpIcon);
            card.Controls.Add(updateButton);
            return card;
        }

        private static Button CreateActionButton(string text, Panel card)
        {
            var button = new Button
            {
                Text = text,
                Width = 104,
                Height = 30,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(card.Width - 122, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = UiPalette.Surface,
                ForeColor = UiPalette.Accent,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = UiPalette.Border;
            button.FlatAppearance.BorderSize = 1;
            card.Resize += delegate
            {
                button.Left = card.ClientSize.Width - button.Width - 18;
            };
            return button;
        }

        private static Image LoadHelpIcon()
        {
            using (Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream("SolidTweaks.Help.png"))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("The Help.png resource was not found.");
                }
                using (Image source = Image.FromStream(stream))
                {
                    return new Bitmap(source);
                }
            }
        }

        private static Image LoadEmbeddedImage(string resourceName)
        {
            using (Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("The resource " + resourceName + " was not found.");
                }
                using (Image source = Image.FromStream(stream))
                {
                    return new Bitmap(source);
                }
            }
        }

        private StatusStrip BuildStatusStrip(out ToolStripStatusLabel label)
        {
            label = new ToolStripStatusLabel
            {
                Text = UiText.Get("Initializing..."),
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiPalette.Muted,
                IsLink = true,
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = UiPalette.Muted,
                ActiveLinkColor = UiPalette.Accent,
                VisitedLinkColor = UiPalette.Muted,
                ToolTipText = UiText.Get("Open the log folder")
            };
            var strip = new StatusStrip
            {
                BackColor = UiPalette.Surface,
                SizingGrip = false,
                ShowItemToolTips = true,
                Padding = new Padding(18, 0, 0, 0)
            };
            strip.Items.Add(label);
            return strip;
        }

        private T TrackText<T>(T control, string english) where T : Control
        {
            localizedTexts[control] = english;
            return control;
        }

        private void TrackAccessibleName(Control control, string english)
        {
            localizedAccessibleNames[control] = english;
        }

        private void TrackToolTip(Control control, string english)
        {
            localizedToolTips[control] = english;
            helpToolTip.SetToolTip(control, UiText.Get(english));
        }

        private static string CurrentLanguageDisplayName()
        {
            return UiLanguageSelector.Current == UiLanguage.Portuguese ? "Português" : "English";
        }

        private void LanguageLinkOnLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            UiLanguage requested = UiLanguageSelector.Current == UiLanguage.Portuguese
                ? UiLanguage.English
                : UiLanguage.Portuguese;
            try
            {
                UiLanguageSelector.SetCurrent(requested);
                ApplyLocalizedUi();
                ApplicationLog.Info("User interface language changed to " + requested + ".");
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("Could not persist the user interface language.", exception);
                MessageBox.Show(
                    this,
                    UiLanguageSelector.Current == UiLanguage.Portuguese
                        ? "Não foi possível salvar a preferência de idioma."
                        : "The language preference could not be saved.",
                    "Solid Tweaks",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void ApplyLocalizedUi()
        {
            foreach (KeyValuePair<Control, string> binding in localizedTexts)
            {
                binding.Key.Text = UiText.Get(binding.Value);
            }
            foreach (KeyValuePair<Control, string> binding in localizedAccessibleNames)
            {
                binding.Key.AccessibleName = UiText.Get(binding.Value);
            }
            foreach (KeyValuePair<Control, string> binding in localizedToolTips)
            {
                helpToolTip.SetToolTip(binding.Key, UiText.Get(binding.Value));
            }
            foreach (KeyValuePair<Control, Control> binding in helpIconBindings)
            {
                Size preferredSize = binding.Key.GetPreferredSize(Size.Empty);
                binding.Value.Left = binding.Key.Left + preferredSize.Width + 8;
            }
            foreach (KeyValuePair<Control, Label> binding in requirementPositionBindings)
            {
                binding.Value.Left = binding.Key.Right + 9;
            }
            foreach (KeyValuePair<Control, Control> binding in helpAccessibleNameBindings)
            {
                binding.Value.AccessibleName = UiText.Format("Help: {0}", binding.Key.Text);
            }

            helpToolTip.ToolTipTitle = UiText.Get("Help");
            languageLink.Text = CurrentLanguageDisplayName();
            RefreshShortcutLabels();
            solidWorksValue.Text = solidWorks.Version;

            bool connected = solidWorks.IsConnected;
            bool hasActiveDocument = connected && solidWorks.HasActiveDocument;
            UpdateRequirementIndicators(connected, hasActiveDocument);
            ApplyConnectionState(connected);
        }

        private async Task InitializeAsync()
        {
            ApplicationLog.Info("Initializing preferences and system information.");
            syncingUi = true;
            exportStepCheckBox.Checked = exportStep.Read();
            exportSheetMetalDxfCheckBox.Checked = exportSheetMetalDxf.Read();
            RefreshShortcutLabels();
            syncingUi = false;
            SetStatus(UiText.Get("Reading system information..."), UiPalette.Muted);
            Task<SystemInformationSnapshot> infoTask = Task.Run(() => systemInfo.Read());
            await RefreshConnectionAsync();
            SystemInformationSnapshot info = await infoTask;
            gpuValue.Text = info.GraphicsCard;
            dotNetValue.Text = info.DotNetFramework;
            ApplicationLog.Info("User interface initialization completed.");
        }

        private async Task RefreshConnectionAsync(bool showProgress = true)
        {
            if (connectionRefreshInProgress)
            {
                return;
            }

            connectionRefreshInProgress = true;
            if (showProgress)
            {
                SetStatus(UiText.Get("Looking for an active SOLIDWORKS session..."), UiPalette.Muted);
            }
            try
            {
                bool connected = solidWorks.TryConnect();
                if (!connected && !showProgress)
                {
                    return;
                }
                ApplyConnectionState(connected);
                lastObservedHasActiveDocument = connected
                    ? (bool?)solidWorks.HasActiveDocument
                    : null;
                if (connected && solidWorks.HasActiveDocument && !shortcutMigration.IsComplete)
                {
                    SetShortcutLinksEnabled(false);
                    SetStatus(UiText.Get("Removing the previous fixed shortcuts..."), UiPalette.Accent);
                    await RunOnStaThread(() => shortcutMigration.Run());
                    RefreshShortcutLabels();
                    ApplyConnectionState(true);
                }
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("Failed to connect to SOLIDWORKS.", exception);
                autoRotateCheckBox.Enabled = false;
                spaceNormalToCheckBox.Enabled = false;
                SetShortcutLinksEnabled(false);
                ApplyUpdateAvailability();
                UpdateRequirementIndicators(false, false);
                solidWorksValue.Text = solidWorks.Version;
                SetStatus(UiText.Format("Connection failed: {0}", FriendlyMessage(exception)), UiPalette.Error);
            }
            finally
            {
                connectionRefreshInProgress = false;
            }
        }

        private async void SolidWorksMonitorTimerOnTick(object sender, EventArgs e)
        {
            if (updateInProgress || IsDisposed || Disposing)
            {
                return;
            }

            solidWorksMonitorTimer.Stop();
            try
            {
                bool processRunning = IsSolidWorksProcessRunning();
                if (!processRunning)
                {
                    solidWorksMonitorTimer.Interval = 2000;
                    lastObservedHasActiveDocument = null;
                    if (solidWorks.IsConnected)
                    {
                        solidWorks.Disconnect();
                        ApplyConnectionState(false);
                        ApplicationLog.Info("SOLIDWORKS closure detected by the session monitor.");
                    }
                    return;
                }

                if (solidWorks.IsConnected && !solidWorks.IsConnectionAlive())
                {
                    lastObservedHasActiveDocument = null;
                }

                if (!solidWorks.IsConnected)
                {
                    solidWorksMonitorTimer.Interval = 1000;
                    await RefreshConnectionAsync(false);
                    if (solidWorks.IsConnected)
                    {
                        solidWorksMonitorTimer.Interval = 3000;
                        ApplicationLog.Info("SOLIDWORKS connected automatically by the session monitor.");
                    }
                    return;
                }

                solidWorksMonitorTimer.Interval = 3000;
                bool hasActiveDocument = solidWorks.HasActiveDocument;
                if (lastObservedHasActiveDocument.HasValue &&
                    lastObservedHasActiveDocument.Value != hasActiveDocument)
                {
                    ApplyConnectionState(true);
                }
                lastObservedHasActiveDocument = hasActiveDocument;
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("SOLIDWORKS session monitor failed.", exception);
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                {
                    solidWorksMonitorTimer.Start();
                }
            }
        }

        private void ApplyConnectionState(bool connected)
        {
            ApplicationLog.Info("Applying connection state. Connected=" + connected + ".");
            solidWorksValue.Text = solidWorks.Version;
            bool hasActiveDocument = connected && solidWorks.HasActiveDocument;
            bool updateAvailable = ApplyUpdateAvailability();
            autoRotateCheckBox.Enabled = connected;
            spaceNormalToCheckBox.Enabled = hasActiveDocument;
            SetShortcutLinksEnabled(hasActiveDocument && shortcutMigration.IsComplete);
            UpdateRequirementIndicators(connected, hasActiveDocument);
            if (!connected)
            {
                SetStatus(
                    updateAvailable
                        ? UiText.Get("An add-in update is available. Click Update.")
                        : UiText.Get("SOLIDWORKS is not open. Open it and click Reconnect."),
                    UiPalette.Warning);
                return;
            }

            try
            {
                syncingUi = true;
                autoRotateCheckBox.Checked = autoRotateSketch.Read();
                spaceNormalToCheckBox.Checked = spaceNormalTo.Read();
                bool exportStepReady = exportStep.Read();
                bool exportDxfReady = exportSheetMetalDxf.Read();
                exportStepCheckBox.Checked = exportStepReady;
                exportSheetMetalDxfCheckBox.Checked = exportDxfReady;
                RefreshShortcutLabels();
                SetStatus(
                    updateAvailable
                        ? UiText.Get("An add-in update is available. Save your projects and click Update.")
                        : exportStepReady || exportDxfReady
                        ? UiText.Get("Connected. Features synchronized.")
                        : UiText.Get("Connected. Select a feature to install or repair the Solid Tweaks tab."),
                    updateAvailable
                        ? UiPalette.Warning
                        : exportStepReady || exportDxfReady ? UiPalette.Success : UiPalette.Warning);
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("Failed to read or synchronize preferences.", exception);
                autoRotateCheckBox.Enabled = false;
                SetStatus(UiText.Format("Could not read the preference: {0}", FriendlyMessage(exception)), UiPalette.Error);
            }
            finally
            {
                syncingUi = false;
            }
        }

        private bool ApplyUpdateAvailability()
        {
            bool available = commandFeatureRegistration.IsUpdateAvailable();
            updateButton.Visible = available;
            updateButton.Enabled = available && !updateInProgress;
            SetUpdateAttentionActive(available && !updateInProgress);
            exportStepCheckBox.Enabled = !available && !updateInProgress;
            exportSheetMetalDxfCheckBox.Enabled = !available && !updateInProgress;
            return available;
        }

        private void SetUpdateAttentionActive(bool active)
        {
            if (active && !SystemInformation.HighContrast)
            {
                if (!updateAttentionTimer.Enabled)
                {
                    updateAttentionStep = 0;
                    updateAttentionTimer.Start();
                    UpdateAttentionTimerOnTick(updateAttentionTimer, EventArgs.Empty);
                }
                return;
            }

            updateAttentionTimer.Stop();
            updateAttentionStep = 0;
            updateButton.BackColor = UiPalette.Surface;
            updateButton.ForeColor = UiPalette.Accent;
            updateButton.FlatAppearance.BorderColor = UiPalette.Border;
        }

        private void UpdateAttentionTimerOnTick(object sender, EventArgs e)
        {
            const int stepsPerCycle = 32;
            double angle = updateAttentionStep * Math.PI * 2.0 / stepsPerCycle;
            double intensity = (Math.Sin(angle - Math.PI / 2.0) + 1.0) / 2.0;
            updateAttentionStep = (updateAttentionStep + 1) % stepsPerCycle;

            Color highlight = Color.FromArgb(219, 234, 254);
            updateButton.BackColor = BlendColor(UiPalette.Surface, highlight, intensity);
            updateButton.ForeColor = BlendColor(UiPalette.Accent, Color.FromArgb(29, 78, 216), intensity);
            updateButton.FlatAppearance.BorderColor = BlendColor(
                UiPalette.Border,
                UiPalette.Accent,
                0.25 + intensity * 0.55);
        }

        private static Color BlendColor(Color from, Color to, double amount)
        {
            amount = Math.Max(0.0, Math.Min(1.0, amount));
            return Color.FromArgb(
                (int)Math.Round(from.R + (to.R - from.R) * amount),
                (int)Math.Round(from.G + (to.G - from.G) * amount),
                (int)Math.Round(from.B + (to.B - from.B) * amount));
        }

        private void AutoRotateCheckBoxOnCheckedChanged(object sender, EventArgs e)
        {
            if (syncingUi)
            {
                return;
            }

            bool requestedValue = autoRotateCheckBox.Checked;
            autoRotateCheckBox.Enabled = false;
            SetStatus(requestedValue ? UiText.Get("Enabling automatic rotation...") : UiText.Get("Disabling automatic rotation..."), UiPalette.Accent);

            try
            {
                if (requestedValue)
                {
                    autoRotateSketch.Apply();
                }
                else
                {
                    autoRotateSketch.Revert();
                }
                bool confirmedValue = autoRotateSketch.Read();
                if (confirmedValue != requestedValue)
                {
                    throw new UserFacingException(
                        "The confirmation returned a value different from the requested value.",
                        UiText.Get("The requested change could not be confirmed."));
                }

                SetStatus(requestedValue ? UiText.Get("Automatic rotation enabled.") : UiText.Get("Automatic rotation disabled."), UiPalette.Success);
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("Failed to change automatic rotation.", exception);
                syncingUi = true;
                autoRotateCheckBox.Checked = !requestedValue;
                syncingUi = false;
                SetStatus(UiText.Format("Could not apply the change: {0}", FriendlyMessage(exception)), UiPalette.Error);
            }
            finally
            {
                autoRotateCheckBox.Enabled = solidWorks.IsConnected;
                bool connected = solidWorks.IsConnected;
                UpdateRequirementIndicators(
                    connected,
                    connected && solidWorks.HasActiveDocument);
            }
        }

        private async void SpaceNormalToCheckBoxOnCheckedChanged(object sender, EventArgs e)
        {
            if (syncingUi)
            {
                return;
            }

            bool enabled = spaceNormalToCheckBox.Checked;
            bool previousValue = spaceNormalTo.Read();
            spaceNormalToCheckBox.Enabled = false;
            try
            {
                if (enabled)
                {
                    await RunOnStaThread(() => spaceNormalTo.Apply());
                }
                else
                {
                    await RunOnStaThread(() => spaceNormalTo.Revert());
                }

                if (spaceNormalTo.Read() != enabled)
                {
                    throw StateConfirmationException();
                }
                SetStatus(
                    enabled
                        ? UiText.Get("Permanent shortcut saved: Space runs Normal To.")
                        : UiText.Get("Permanent shortcut saved: Space opens 3D View."),
                    enabled ? UiPalette.Success : UiPalette.Muted);
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("Failed to configure the Space key shortcut.", exception);
                syncingUi = true;
                spaceNormalToCheckBox.Checked = previousValue;
                syncingUi = false;
                SetStatus(UiText.Format("Could not save the shortcut: {0}", FriendlyMessage(exception)), UiPalette.Error);
            }
            finally
            {
                bool connected = solidWorks.IsConnected;
                bool hasActiveDocument = connected && solidWorks.HasActiveDocument;
                spaceNormalToCheckBox.Enabled = hasActiveDocument;
                UpdateRequirementIndicators(connected, hasActiveDocument);
            }
        }

        private async void ConfigureShortcut(ConfigurableShortcutTweak feature, LinkLabel link)
        {
            if (!link.Enabled)
            {
                return;
            }

            ShortcutBinding current = feature.Read();
            using (var dialog = new ShortcutCaptureDialog(ShortcutActionDisplayName(feature.Action), current))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                ShortcutBinding requested = dialog.CapturedBinding;
                SetShortcutLinksEnabled(false);
                try
                {
                    SetStatus(
                        dialog.ClearRequested
                            ? UiText.Get("Removing the shortcut...")
                            : UiText.Format("Saving the {0} shortcut...", SolidWorksKeyboardShortcutService.ShortcutDisplayName(requested)),
                        UiPalette.Accent);
                    await RunOnStaThread(() =>
                    {
                        if (dialog.ClearRequested)
                        {
                            feature.Clear();
                        }
                        else
                        {
                            feature.Assign(requested);
                        }
                    });
                    RefreshShortcutLabels();
                    SetStatus(
                        dialog.ClearRequested
                            ? UiText.Format("Shortcut removed from {0}.", ShortcutActionDisplayName(feature.Action))
                            : UiText.Format(
                                "Shortcut saved: {0} runs {1}.",
                                SolidWorksKeyboardShortcutService.ShortcutDisplayName(requested),
                                ShortcutActionDisplayName(feature.Action)),
                        dialog.ClearRequested ? UiPalette.Muted : UiPalette.Success);
                }
                catch (OperationCanceledException)
                {
                    RefreshShortcutLabels();
                    SetStatus(UiText.Get("Shortcut change canceled."), UiPalette.Muted);
                }
                catch (Exception exception)
                {
                    ApplicationLog.Error("Failed to configure shortcut " + feature.Action + ".", exception);
                    RefreshShortcutLabels();
                    SetStatus(UiText.Format("Could not save the shortcut: {0}", FriendlyMessage(exception)), UiPalette.Error);
                }
                finally
                {
                    bool connected = solidWorks.IsConnected;
                    bool hasActiveDocument = connected && solidWorks.HasActiveDocument;
                    SetShortcutLinksEnabled(hasActiveDocument && shortcutMigration.IsComplete);
                    UpdateRequirementIndicators(connected, hasActiveDocument);
                }
            }
        }

        private void RefreshShortcutLabels()
        {
            fixSelectionShortcutLink.Text = SolidWorksKeyboardShortcutService.ShortcutDisplayName(fixSelectionShortcut.Read());
            constructionGeometryShortcutLink.Text = SolidWorksKeyboardShortcutService.ShortcutDisplayName(constructionGeometryShortcut.Read());
            measureShortcutLink.Text = SolidWorksKeyboardShortcutService.ShortcutDisplayName(measureShortcut.Read());
            offsetEntitiesShortcutLink.Text = SolidWorksKeyboardShortcutService.ShortcutDisplayName(offsetEntitiesShortcut.Read());
        }

        private void SetShortcutLinksEnabled(bool enabled)
        {
            fixSelectionShortcutLink.Enabled = enabled;
            constructionGeometryShortcutLink.Enabled = enabled;
            measureShortcutLink.Enabled = enabled;
            offsetEntitiesShortcutLink.Enabled = enabled;
        }

        private static string ShortcutActionDisplayName(ShortcutAction action)
        {
            switch (action)
            {
                case ShortcutAction.MakeFixed:
                    return UiText.Get("Make Fixed");
                case ShortcutAction.ConstructionGeometry:
                    return UiText.Get("Construction Geometry");
                case ShortcutAction.Measure:
                    return UiText.Get("Measure");
                default:
                    return UiText.Get("Offset Entities");
            }
        }

        private void UpdateRequirementIndicators(bool connected, bool hasActiveDocument)
        {
            SetRequirement(
                autoRotateRequirementLabel,
                connected ? null : UiText.Get("SOLIDWORKS must be open"));

            string spaceRequirement = !connected
                ? UiText.Get("SOLIDWORKS must be open")
                : hasActiveDocument ? null : UiText.Get("An open document is required");
            SetRequirement(spaceNormalToRequirementLabel, spaceRequirement);
            SetRequirement(fixSelectionShortcutRequirementLabel, spaceRequirement);
            SetRequirement(constructionGeometryShortcutRequirementLabel, spaceRequirement);
            SetRequirement(measureShortcutRequirementLabel, spaceRequirement);
            SetRequirement(offsetEntitiesShortcutRequirementLabel, spaceRequirement);

        }

        private static void SetRequirement(Label label, string message)
        {
            label.Text = message ?? string.Empty;
            label.Visible = !string.IsNullOrEmpty(message);
        }

        private void ExportStepCheckBoxOnCheckedChanged(object sender, EventArgs e)
        {
            ChangeCommandFeature(exportStep, exportStepCheckBox);
        }

        private void ExportSheetMetalDxfCheckBoxOnCheckedChanged(object sender, EventArgs e)
        {
            ChangeCommandFeature(exportSheetMetalDxf, exportSheetMetalDxfCheckBox);
        }

        private async void UpdateButtonOnClick(object sender, EventArgs e)
        {
            if (updateInProgress || !commandFeatureRegistration.IsUpdateAvailable())
            {
                return;
            }

            DialogResult confirmation = MessageBox.Show(
                this,
                UiText.Get("Save all open projects before continuing.\n\nSOLIDWORKS will close to update the Solid Tweaks tab and reopen when the update is complete."),
                UiText.Get("Update Solid Tweaks"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.OK)
            {
                ApplicationLog.Info("The user canceled the update before SOLIDWORKS was closed.");
                return;
            }

            ApplicationLog.Info("The user confirmed the update.");
            updateInProgress = true;
            ApplyUpdateAvailability();
            reconnectButton.Enabled = false;
            autoRotateCheckBox.Enabled = false;
            spaceNormalToCheckBox.Enabled = false;
            SetShortcutLinksEnabled(false);
            string executablePath = null;
            bool closedByUpdater = false;
            bool restarted = false;
            try
            {
                executablePath = solidWorks.GetExecutablePath();
                bool connected = solidWorks.IsConnected || solidWorks.TryConnect();
                if (!connected && IsSolidWorksProcessRunning())
                {
                    throw new UserFacingException(
                        "SOLIDWORKS is running but did not accept the connection required to close it safely.",
                        UiText.Get("SOLIDWORKS is running but did not accept the connection required to close it safely."));
                }

                if (connected)
                {
                    SetStatus(UiText.Get("Closing SOLIDWORKS for the update..."), UiPalette.Accent);
                    Refresh();
                    int processId = solidWorks.ExitForUpdate();
                    ApplicationLog.Info("SOLIDWORKS shutdown requested. PID=" + processId + ".");
                    closedByUpdater = true;
                    if (!await WaitForProcessExitAsync(processId, 60000))
                    {
                        throw new UserFacingException(
                            "SOLIDWORKS did not close within the expected time. The update was canceled.",
                            UiText.Get("SOLIDWORKS did not close within the expected time. The update was canceled."));
                    }
                }

                SetStatus(UiText.Get("Installing the new add-in version..."), UiPalette.Accent);
                Refresh();
                await Task.Run(() => commandFeatureRegistration.UpdateInstalledAddin());
                ApplicationLog.Info("New DLL registration completed; starting SOLIDWORKS again.");

                SetStatus(UiText.Get("Update installed. Reopening SOLIDWORKS..."), UiPalette.Accent);
                StartSolidWorks(executablePath);
                restarted = true;

                syncingUi = true;
                exportStepCheckBox.Checked = exportStep.Read();
                exportSheetMetalDxfCheckBox.Checked = exportSheetMetalDxf.Read();
                RefreshShortcutLabels();
                syncingUi = false;
                ApplyUpdateAvailability();

                if (await WaitForUpdatedSolidWorksAsync(90000))
                {
                    ApplicationLog.Info("SOLIDWORKS reopened and the add-in interface was confirmed.");
                    ApplyConnectionState(true);
                    SetStatus(UiText.Get("Solid Tweaks was updated successfully. Previous features were restored."), UiPalette.Success);
                }
                else
                {
                    ApplicationLog.Warning("SOLIDWORKS reopened, but the add-in interface was not confirmed in time.");
                    SetStatus(
                        UiText.Get("The update was installed, but the tab could not yet be confirmed in SOLIDWORKS."),
                        UiPalette.Error);
                }
            }
            catch (Exception exception)
            {
                ApplicationLog.Error(
                    "Update failed. closedByUpdater=" + closedByUpdater +
                    "; restarted=" + restarted +
                    "; executable=" + (executablePath ?? "<not obtained>") + ".",
                    exception);
                if (closedByUpdater && !restarted && !string.IsNullOrWhiteSpace(executablePath))
                {
                    try
                    {
                        StartSolidWorks(executablePath);
                    }
                    catch
                    {
                        ApplicationLog.Error("SOLIDWORKS also could not be reopened after the failure.");
                        // Mantém a mensagem da falha original da atualização.
                    }
                }
                SetStatus(UiText.Format("Could not update: {0}", FriendlyMessage(exception)), UiPalette.Error);
            }
            finally
            {
                syncingUi = false;
                updateInProgress = false;
                reconnectButton.Enabled = true;
                ApplyUpdateAvailability();
                bool connected = solidWorks.IsConnected;
                autoRotateCheckBox.Enabled = connected;
                spaceNormalToCheckBox.Enabled = connected && solidWorks.HasActiveDocument;
                SetShortcutLinksEnabled(connected && solidWorks.HasActiveDocument && shortcutMigration.IsComplete);
            }
        }

        private async Task<bool> WaitForUpdatedSolidWorksAsync(int timeoutMilliseconds)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < timeoutMilliseconds)
            {
                await Task.Delay(1000);
                if (solidWorks.TryConnect() && commandFeatureRegistration.IsInstalledInterfaceReady())
                {
                    return true;
                }
            }
            return false;
        }

        private static Task<bool> WaitForProcessExitAsync(int processId, int timeoutMilliseconds)
        {
            return Task.Run(delegate
            {
                try
                {
                    using (Process process = Process.GetProcessById(processId))
                    {
                        return process.WaitForExit(timeoutMilliseconds);
                    }
                }
                catch (ArgumentException)
                {
                    return true;
                }
            });
        }

        private static bool IsSolidWorksProcessRunning()
        {
            Process[] processes = Process.GetProcessesByName("SLDWORKS");
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }

        private static void StartSolidWorks(string executablePath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true
            });
        }

        private void ChangeCommandFeature(ITweak feature, CheckBox checkBox)
        {
            if (syncingUi)
            {
                return;
            }

            bool requestedValue = checkBox.Checked;
            bool previousValue = feature.Read();
            exportStepCheckBox.Enabled = false;
            exportSheetMetalDxfCheckBox.Enabled = false;
            SetStatus(
                requestedValue
                    ? UiText.Format("Adding {0} to the Solid Tweaks tab...", feature.DisplayName)
                    : UiText.Format("Removing {0} from the Solid Tweaks tab...", feature.DisplayName),
                UiPalette.Accent);
            Refresh();

            try
            {
                if (requestedValue)
                {
                    feature.Apply();
                }
                else
                {
                    feature.Revert();
                }
                if (feature.Read() != requestedValue)
                {
                    throw new UserFacingException(
                        "The feature state could not be confirmed: " + feature.Id + ".",
                        UiText.Format("The state of {0} could not be confirmed.", feature.DisplayName));
                }

                syncingUi = true;
                exportStepCheckBox.Checked = exportStep.Read();
                exportSheetMetalDxfCheckBox.Checked = exportSheetMetalDxf.Read();
                syncingUi = false;
                bool anyFeature = exportStepCheckBox.Checked || exportSheetMetalDxfCheckBox.Checked;
                SetStatus(
                    requestedValue
                        ? UiText.Format("{0} was added to the Solid Tweaks tab.", feature.DisplayName)
                        : anyFeature
                            ? UiText.Format("{0} was removed; the tab still contains the other features.", feature.DisplayName)
                            : UiText.Get("The last feature was removed; the Solid Tweaks tab was deleted."),
                    requestedValue ? UiPalette.Success : UiPalette.Muted);
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("Failed to change tab feature " + feature.Id + ".", exception);
                syncingUi = true;
                checkBox.Checked = previousValue;
                syncingUi = false;
                SetStatus(UiText.Format("Could not change the tab: {0}", FriendlyMessage(exception)), UiPalette.Error);
            }
            finally
            {
                ApplyUpdateAvailability();
            }
        }

        private void ReportKeyboardProgress(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(ReportKeyboardProgress), message);
                return;
            }

            SetStatus(message, UiPalette.Accent);
            Refresh();
        }

        private static Task RunOnStaThread(Action action)
        {
            var completion = new TaskCompletionSource<object>();
            var thread = new Thread(delegate()
            {
                try
                {
                    action();
                    completion.SetResult(null);
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private void SetStatus(string message, Color color)
        {
            statusLabel.Text = message;
            statusLabel.ForeColor = color;
            statusLabel.LinkColor = color;
            statusLabel.ActiveLinkColor = color;
            statusLabel.VisitedLinkColor = color;
            statusLabel.ToolTipText = color == UiPalette.Error
                ? message + Environment.NewLine + UiText.Format("Log: {0}", LocalizedLogPath())
                : UiText.Format("Open log folder: {0}", Path.GetDirectoryName(LocalizedLogPath()));
            if (color == UiPalette.Error)
            {
                ApplicationLog.Error("An error status was displayed to the user.");
            }
        }

        private void StatusLabelOnClick(object sender, EventArgs e)
        {
            string logPath = LocalizedLogPath();
            string directory = Path.GetDirectoryName(logPath);
            try
            {
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                {
                    throw new UserFacingException(
                        "The log folder is not available yet.",
                        UiText.Get("The log folder is not available yet."));
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true
                });
                ApplicationLog.Info("Log folder opened from the status bar: " + directory);
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("Could not open the log folder.", exception);
                MessageBox.Show(
                    this,
                    UiText.Format("Could not open the log folder.\n\n{0}\n\nPath: {1}", FriendlyMessage(exception), logPath),
                    "Solid Tweaks",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OpenChannelOnClick(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ChannelUrl,
                    UseShellExecute = true
                });
                ApplicationLog.Info("CG3D channel opened: " + ChannelUrl);
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("Could not open the CG3D channel.", exception);
                MessageBox.Show(
                    this,
                    UiText.Format("Could not open the CG3D channel.\n\n{0}\n\n{1}", FriendlyMessage(exception), ChannelUrl),
                    "Solid Tweaks",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OpenRepositoryOnClick(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = RepositoryUrl,
                    UseShellExecute = true
                });
                ApplicationLog.Info("SolidTweaks repository opened: " + RepositoryUrl);
            }
            catch (Exception exception)
            {
                ApplicationLog.Error("Could not open the SolidTweaks repository.", exception);
                MessageBox.Show(
                    this,
                    UiText.Format("Could not open the SolidTweaks repository.\n\n{0}\n\n{1}", FriendlyMessage(exception), RepositoryUrl),
                    "Solid Tweaks",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static string FriendlyMessage(Exception exception)
        {
            return UiText.UserMessage(exception);
        }

        private static string LocalizedLogPath()
        {
            string path = ApplicationLog.CurrentPath;
            return string.Equals(path, "Log unavailable", StringComparison.Ordinal)
                ? UiText.Get("Log unavailable")
                : path;
        }

        private static UserFacingException StateConfirmationException()
        {
            return new UserFacingException(
                "The persisted state does not match the requested change.",
                UiText.Get("The saved state does not match the requested change."));
        }

        private static Panel CreateCard(int height)
        {
            var card = new Panel
            {
                Width = 694,
                Height = height,
                BackColor = UiPalette.Surface,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(18)
            };
            card.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(UiPalette.Border))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                }
            };
            return card;
        }

        private static Label CreateSectionTitle(string text, int top)
        {
            return new Label
            {
                AutoSize = true,
                Location = new Point(18, top),
                Text = text,
                Font = new Font("Segoe UI Semibold", 8.5F),
                ForeColor = UiPalette.Accent
            };
        }

        private static Label CreateCaptionLabel(string text, int top)
        {
            return new Label
            {
                AutoSize = false,
                Location = new Point(18, top),
                Size = new Size(126, 22),
                Text = text,
                Font = new Font("Segoe UI Semibold", 8F),
                ForeColor = UiPalette.Muted
            };
        }

        private static Label CreateValueLabel(string text, int top)
        {
            return new Label
            {
                AutoEllipsis = true,
                Location = new Point(150, top),
                Size = new Size(510, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Text = text,
                ForeColor = UiPalette.Text
            };
        }
    }
}
