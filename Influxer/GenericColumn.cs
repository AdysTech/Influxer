using AdysTech.Influxer.Config;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdysTech.Influxer
{
    class GenericColumn : ITransform
    {

        public int ColumnIndex { get; set; }
        public string ColumnHeader { get; set; }
        public ColumnDataType Type { get; set; }
        public ColumnConfig Config { get; set; }


        List<ITransform> _transformations;

        private void GetTransformations()
        {
            _transformations = new List<ITransform>();

            if (Config == null) return;
            foreach (var property in Config.GetType().GetProperties())
            {
                if (property.PropertyType.BaseType.IsGenericType)
                {
                    Type t = property.PropertyType.BaseType;
                    Type[] typeParameters = t.GetGenericArguments();

                    foreach (Type tParam in typeParameters)
                    {
                        if (typeof(ITransform).IsAssignableFrom(tParam))
                        {
                            var list = property.GetValue(this.Config) as ICollection;
                            if (list != null && list.Count > 0)
                                foreach (ITransform transform in list)
                                    _transformations.Add(transform);
                        }
                    }

                }

            }
        }

        public bool HasTransformations
        {
            get
            {
                if (_transformations == null)
                    GetTransformations();
                return (_transformations.Count > 0);
            }
        }

        public bool CanTransform(string content)
        {
            if (!HasTransformations)
                return false;
            return _transformations.Any(t => t.CanTransform(content) == true);
        }

        public string Transform(string content)
        {
            if (HasTransformations)
            {
                var transformation = _transformations.FirstOrDefault(t => t.CanTransform(content) == true);
                if (transformation != null)
                    return transformation.Transform(content);
            }
            return content;
        }
    }
}
