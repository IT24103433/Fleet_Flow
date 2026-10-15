using SkiaSharp;

namespace IdentityService.Api.Onboarding;

public static class DocumentImage
{
    public static byte[] Sanitize(byte[] bytes)
    {
        using var stream=new SKMemoryStream(bytes);using var codec=SKCodec.Create(stream);
        if(codec==null||codec.Info.Width<=0||codec.Info.Height<=0||(long)codec.Info.Width*codec.Info.Height>20_000_000||codec.FrameCount>1)
            throw new OnboardingException(400,"Choose a readable, static document photograph up to 20 megapixels.");
        using var source=SKBitmap.Decode(codec);if(source==null)throw new OnboardingException(400,"This photograph could not be decoded.");
        var origin=(int)codec.EncodedOrigin;var swap=origin>=5;var w=source.Width;var h=source.Height;
        using var bitmap=new SKBitmap(swap?h:w,swap?w:h);using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.White);
        var m=new SKMatrix{ScaleX=1,ScaleY=1,Persp2=1};
        switch(origin)
        {
            case 2:m.ScaleX=-1;m.TransX=w;break;
            case 3:m.ScaleX=-1;m.ScaleY=-1;m.TransX=w;m.TransY=h;break;
            case 4:m.ScaleY=-1;m.TransY=h;break;
            case 5:m.ScaleX=0;m.ScaleY=0;m.SkewX=1;m.SkewY=1;break;
            case 6:m.ScaleX=0;m.ScaleY=0;m.SkewX=-1;m.SkewY=1;m.TransX=h;break;
            case 7:m.ScaleX=0;m.ScaleY=0;m.SkewX=-1;m.SkewY=-1;m.TransX=h;m.TransY=w;break;
            case 8:m.ScaleX=0;m.ScaleY=0;m.SkewX=1;m.SkewY=-1;m.TransY=w;break;
        }
        canvas.SetMatrix(m);canvas.DrawBitmap(source,0,0,new SKSamplingOptions(SKFilterMode.Nearest),null);canvas.Flush();
        using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Jpeg,95);
        var output=data.ToArray();if(output.Length>5*1024*1024)throw new OnboardingException(400,"This photograph is too large after safe processing. Choose a smaller image.");
        return output;
    }
}
