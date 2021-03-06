﻿using BeiDream.Core.Domain.Entities;
using BeiDream.Core.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using BeiDream.Core.Security;

namespace BeiDream.Data.Ef.Repositories
{
    public abstract class Repository<TAggregateRoot> : Repository<TAggregateRoot, Guid>
        where TAggregateRoot : class, IAggregateRoot<Guid>
    {
        protected Repository(IDbContext dbContext)
            : base(dbContext)
        {
        }
        public override void Update(TAggregateRoot entity)
        {
            if (!AttachIfNot(entity))
            {
                TAggregateRoot oldEntity = GetAllAsNoTracking().SingleOrDefault(p => p.Id == entity.Id);
                if(entity==null)
                    throw new ArgumentNullException("更新的数据不存在");
                ValidateVersion(entity, oldEntity);
            }
            DbContext.Entry(entity).State = EntityState.Modified;
        }
    }

    public abstract class Repository<TAggregateRoot, TKey> : IRepository<TAggregateRoot, TKey> where TAggregateRoot : class, IAggregateRoot<TKey>
    {
        protected IDbContext DbContext { get; private set; }
        /// <summary>
        /// 应用程序上下文
        /// </summary>
        protected IApplicationSession ApplicationSession;
        protected Repository(IDbContext dbContext)
        {
            ApplicationSession = GetApplicationContext();
            DbContext = dbContext;
        }

        private DbSet<TAggregateRoot> Set
        {
            get { return DbContext.Set<TAggregateRoot>(); }
        }

        public void Add(TAggregateRoot entity)
        {
            Set.Add(entity);
        }

        public TAggregateRoot AddEntity(TAggregateRoot entity)
        {
            var addEntity= Set.Add(entity);
            DbContext.SaveChanges();
            return addEntity;
        }

        public virtual void Update(TAggregateRoot entity)
        {
            AttachIfNot(entity);
            DbContext.Entry(entity).State = EntityState.Modified;
        }
        //验证版本号
        protected void ValidateVersion(TAggregateRoot newEntity, TAggregateRoot oldEntity)
        {
            if (newEntity.Version == null)
                throw new DbUpdateConcurrencyException("当前数据已被修改,请刷新后重试");
            for (int i = 0; i < oldEntity.Version.Length; i++)
            {
                if (newEntity.Version[i] != oldEntity.Version[i])
                    throw new DbUpdateConcurrencyException("当前数据已被修改,请刷新后重试");
            }
        }
        #region 删除操作

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="ignoreSoftDelete">忽略软删除功能(默认不忽略,如果设置为true,即使继承了ISoftDelete接口一样物理删除)</param>
        public void Delete(TAggregateRoot entity, bool ignoreSoftDelete = false)
        {
            if (entity is ISoftDelete && !ignoreSoftDelete)
            {
                (entity as ISoftDelete).IsDeleted = true;
            }
            else
            {
                Set.Remove(entity);
            }

        }

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <param name="ignoreSoftDelete">忽略软删除功能(默认不忽略,如果设置为true,即使继承了ISoftDelete接口一样物理删除)</param>
        public void Delete(IEnumerable<TAggregateRoot> entities, bool ignoreSoftDelete = false)
        {
            if (entities == null)
                return;
            var list = entities.ToList();
            if (!list.Any())
                return;
            foreach (var entity in list)
            {
                Delete(entity,ignoreSoftDelete);
            }
        }
        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="predicate">删掉条件</param>
        /// <param name="ignoreSoftDelete">忽略软删除功能(默认不忽略,如果设置为true,即使继承了ISoftDelete接口一样物理删除)</param>
        public void Delete(Expression<Func<TAggregateRoot, bool>> predicate, bool ignoreSoftDelete = false)
        {
            var entities = Set.Where(predicate);
            Delete(entities, ignoreSoftDelete);
        } 
        #endregion

        public TAggregateRoot Find(TKey id)
        {
            return Set.Find(id);
        }
        /// <summary>
        /// 获取所有数据
        /// </summary>
        /// <returns></returns>
        public IQueryable<TAggregateRoot> GetAll()
        {
            return Set;
        }
        /// <summary>
        /// 获取未跟踪的实体集
        /// </summary>
        public IQueryable<TAggregateRoot> GetAllAsNoTracking()
        {
            return Set.AsNoTracking();
        }
        /// <summary>
        /// 获取过滤数据权限的所有数据
        /// </summary>
        /// <returns></returns>
        public IQueryable<TAggregateRoot> GetAllFilterDataPermissions()
        {
            if (ApplicationSession.IsAdmin)
                return Set;
            return Set.Where(GetDataPermissions());
        }
        /// <summary>
        /// 获取应用程序上下文
        /// </summary>
        protected IApplicationSession GetApplicationContext()
        {
            return ApplicationSession ?? (ApplicationSession = Core.Security.ApplicationSession.Current);
        }
        /// <summary>
        /// 获取数据权限查询条件
        /// </summary>
        protected virtual Expression<Func<TAggregateRoot, bool>> GetDataPermissions()
        {
            return p => true;   
        }

        protected virtual bool AttachIfNot(TAggregateRoot entity)
        {
            if (!Set.Local.Contains(entity))
            {
                Set.Attach(entity);
                return true;
            }
            return false;
        }
    }
}