< UserControl x: Class = "F76World.Launcher.UI.AudioDeviceSwitcher"
             xmlns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns: x = "http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns: mc = "http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns: d = "http://schemas.microsoft.com/expression/blend/2008"
             mc: Ignorable = "d"
             d: DesignHeight = "150" d: DesignWidth = "400"
             Background = "#181825" Foreground = "#cdd6f4" >

    < !--Mojave UI Styling embedded for the component -->
    <UserControl.Resources>
        <Style TargetType="TextBlock">
            <Setter Property="FontFamily" Value="Segoe UI Semibold"/>
        </Style>
        <Style TargetType="ComboBox">
            <Setter Property="Background" Value="#313244"/>
            <Setter Property="Foreground" Value="#cdd6f4"/>
            <Setter Property="BorderBrush" Value="#45475a"/>
            <Setter Property="Padding" Value="5"/>
        </Style>
        <Style TargetType="Button">
            <Setter Property="Background" Value="#89b4fa"/>
            <Setter Property="Foreground" Value="#11111b"/>
            <Setter Property="FontWeight" Value="Bold"/>
            <Setter Property="Padding" Value="10,5"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#b4befe"/>
                </Trigger>
            </Style.Triggers>
        </Style>
    </UserControl.Resources>

    <Border BorderBrush="#313244" BorderThickness="1" CornerRadius="8" Padding="15">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <StackPanel Orientation="Horizontal" Grid.Row="0" Margin="0,0,0,10">
                <TextBlock Text="🔈 AUDIO SENTINEL ROUTING" Foreground="#a6e3a1" FontSize="14"/>
                <Ellipse x:Name = "StatusIndicator" Width = "10" Height = "10" Fill = "#f38ba8" Margin = "10,2,0,0" />
                < TextBlock x: Name = "StatusText" Text = "Offline" Foreground = "#a6adc8" FontSize = "12" Margin = "5,0,0,0" VerticalAlignment = "Center" />
            </ StackPanel >

            < TextBlock Grid.Row = "1" Text = "Select Target Playback Device:" FontSize = "12" Foreground = "#a6adc8" Margin = "0,0,0,5" />


            < ComboBox x: Name = "DeviceComboBox" Grid.Row = "2" DisplayMemberPath = "Name" SelectedValuePath = "Id" Height = "30" Margin = "0,0,0,15" DropDownOpened = "DeviceComboBox_DropDownOpened" />

            < Grid Grid.Row = "3" >
                < Grid.ColumnDefinitions >
                    < ColumnDefinition Width = "*" />
                    < ColumnDefinition Width = "10" />
                    < ColumnDefinition Width = "*" />
                </ Grid.ColumnDefinitions >

                < Button x: Name = "ApplyRoutingBtn" Grid.Column = "0" Content = "APPLY ROUTING" Click = "ApplyRoutingBtn_Click" />
                < Button x: Name = "RestartAudioBtn" Grid.Column = "2" Content = "RESTART AUDIO ENGINE" Background = "#f38ba8" Click = "RestartAudioBtn_Click" />
            </ Grid >
        </ Grid >
    </ Border >
</ UserControl >