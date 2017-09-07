using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KungHua.WeChat
{
    /// <summary>
    /// 全局配置
    /// </summary>
    public class WeChatConfig
    {
        #region 全局配置的单例模式
        private static WeChatConfig _weChatConfig;
        private static string locker;
        public static WeChatConfig()
        {
            locker = "wechatconfig";
        }
        public static WeChatConfig Ins()
        {
            if (_weChatConfig == null)
            {
                lock (locker)
                {
                    if (_weChatConfig == null)
                    {
                        _weChatConfig = new WeChatConfig();
                    }
                }
            }
            return _weChatConfig;
        }
        #endregion



        #region 属性
        public string AppId { get; set; }
        public string Appsecret { get; set; }
        public string Token { get; set; }
        #endregion
    }
}
