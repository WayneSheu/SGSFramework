using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace SGSFramework.Core.Controllers.Conventions
{
    /// <summary>
    /// API 版本路由慣例，用於在控制器路徑前加上指定的 API 版本前綴。
    /// </summary>
    public class ApiVersionRouteConvention : IApplicationModelConvention
    {
        private readonly string _prefix;

        public ApiVersionRouteConvention(string prefix)
        {
            _prefix = prefix;
        }

        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                // 建立新的路由模型：將原本的路由前面加上指定的版號前綴
                var hauledRoute = new AttributeRouteModel
                {
                    Template = $"{_prefix}/[controller]"
                };

                // 如果 Controller 本身已經有指定 Attribute Route，可選擇進行組合或全面取代
                foreach (var selector in controller.Selectors)
                {
                    if (selector.AttributeRouteModel != null)
                    {
                        selector.AttributeRouteModel.Template = $"{_prefix}/{selector.AttributeRouteModel.Template}";
                    }
                    else
                    {
                        selector.AttributeRouteModel = hauledRoute;
                    }
                }
            }
        }
    }

}
