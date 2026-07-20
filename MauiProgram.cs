using KrakenMobile.Services;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;

namespace KrakenMobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		try
		{
			return BuildApp();
		}
		catch (Exception ex)
		{
			BootFail(ex);
			throw;
		}
	}

	private static MauiApp BuildApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

#if ANDROID
	private static void BootFail(Exception ex)
	{
		var text = ex.ToString();

		try
		{
			var dir = Android.App.Application.Context?.GetExternalFilesDir(null);
			if (dir != null)
				System.IO.File.WriteAllText(System.IO.Path.Combine(dir.AbsolutePath, "mauiboot.txt"), text);
		}
		catch { }

		try
		{
			var ctx = (Android.Content.Context?)MainActivity.CurrentActivity ?? Android.App.Application.Context;
			var activity = ctx as Android.App.Activity;
			var show = new Action(() =>
			{
				try
				{
					var b = new Android.App.AlertDialog.Builder(ctx!);
					b.SetTitle("Maui Boot Failed");
					b.SetMessage(text.Length > 3000 ? text.Substring(0, 3000) : text);
					b.SetPositiveButton("OK", (s, a) => { });
					b.Create().Show();
				}
				catch { }
			});
			if (activity != null) activity.RunOnUiThread(show); else show();
		}
		catch { }
	}
#endif
}
