namespace QDX.Serivices.HttpService
{
    public class CommonResponse :BaseResponse
    {
        public object? Data { get; set; }


        public static RestResponse<T> Ok<T>(T data)
        {
            return new RestResponse<T> {
                Code=0,
                Message="成功",
                Data = data };

        }

        public static  RestResponse<T> Fail<T>(int code, string message)
        {
            return new RestResponse<T>
            {
                Code = code,
                Message = message,
            };


        }
        public static RestResponse<T> Fail<T>(string message)
        {
            return new RestResponse<T>
            {
                Code = -1,
                Message = message,
            };
        }
    }
}
