namespace QDX.Serivices.HttpService
{
    public class RestResponse<T> :CommonResponse
    {
        public new T? Data { get; set; }
    }
}
