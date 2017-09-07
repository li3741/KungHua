using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KungHua.WeChat
{
    /// <summary>
    /// 定义微信通用常量url,即基本请求url
    /// </summary>
    public class WeChatUrl
    {
        /// <summary>
        /// access_token是公众号的全局唯一接口调用凭据，公众号调用各接口时都需使用access_token。
        /// 开发者需要进行妥善保存。access_token的存储至少要保留512个字符空间。
        /// access_token的有效期目前为2个小时，需定时刷新，重复获取将导致上次获取的access_token失效。
        /// </summary>
        public static readonly string ACCESS_TOKEN = @"https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={APPID}&secret={APPSECRET}";

    }
}
